#!/usr/bin/env python3
"""Сборка ipk для Entware и индекса feed opkg.

ipk собирается в формате OpenWrt/Entware: tar.gz с ./debian-binary, ./data.tar.gz, ./control.tar.gz.
Сборка воспроизводима: владелец 0:0, общее время изменения, gzip без имени и времени.

  ipk.py build --control DIR --data DIR --version X.Y.Z --out DIR
  ipk.py index --feed DIR
"""

import argparse
import gzip
import hashlib
import io
import os
import re
import sys
import tarfile

ARCHITECTURE = "aarch64-3.10"
CONTROL_SCRIPTS = {"preinst", "postinst", "prerm", "postrm"}
EXECUTABLE_DIRS = ("opt/bin/", "opt/sbin/", "opt/etc/init.d/")
VERSION_PATTERN = re.compile(r"^\d+\.\d+\.\d+(-\d+)?$")


def mtime() -> int:
    return int(os.environ.get("SOURCE_DATE_EPOCH", "0"))


def is_executable(relative: str) -> bool:
    return relative.startswith(EXECUTABLE_DIRS) or relative.endswith(".sh")


def gzip_bytes(data: bytes) -> bytes:
    buffer = io.BytesIO()
    with gzip.GzipFile(filename="", mode="wb", compresslevel=9, fileobj=buffer, mtime=0) as archive:
        archive.write(data)
    return buffer.getvalue()


def add_bytes(archive: tarfile.TarFile, name: str, data: bytes, mode: int) -> None:
    info = tarfile.TarInfo(name)
    info.size = len(data)
    info.mode = mode
    info.mtime = mtime()
    info.uid = info.gid = 0
    info.uname = info.gname = "root"
    archive.addfile(info, io.BytesIO(data))


def add_directory(archive: tarfile.TarFile, name: str) -> None:
    info = tarfile.TarInfo(name)
    info.type = tarfile.DIRTYPE
    info.mode = 0o755
    info.mtime = mtime()
    info.uid = info.gid = 0
    info.uname = info.gname = "root"
    archive.addfile(info)


def read_text_file(path: str) -> bytes:
    data = open(path, "rb").read()
    if b"\r" in data:
        sys.exit(f"{path}: строки с CRLF, нужен LF")
    return data


def tar_gz(entries) -> bytes:
    buffer = io.BytesIO()
    with tarfile.open(fileobj=buffer, mode="w", format=tarfile.GNU_FORMAT) as archive:
        for kind, name, data, mode in entries:
            if kind == "dir":
                add_directory(archive, name)
            else:
                add_bytes(archive, name, data, mode)
    return gzip_bytes(buffer.getvalue())


def data_entries(data_dir: str):
    entries = [("dir", "./", None, 0)]
    installed_size = 0
    for root, dirs, files in os.walk(data_dir):
        dirs.sort()
        relative_root = os.path.relpath(root, data_dir).replace(os.sep, "/")
        prefix = "" if relative_root == "." else relative_root + "/"
        for directory in dirs:
            entries.append(("dir", "./" + prefix + directory + "/", None, 0))
        for file in sorted(files):
            relative = prefix + file
            path = os.path.join(root, file)
            executable = is_executable(relative)
            script = executable and not relative.startswith(("opt/bin/", "opt/sbin/"))
            data = read_text_file(path) if script else open(path, "rb").read()
            installed_size += len(data)
            entries.append(("file", "./" + relative, data, 0o755 if executable else 0o644))
    entries.sort(key=lambda entry: entry[1])
    return entries, installed_size


def build(arguments) -> None:
    if not VERSION_PATTERN.match(arguments.version):
        sys.exit(f"версия {arguments.version!r} не в формате X.Y.Z")
    data, installed_size = data_entries(arguments.data)
    template = read_text_file(os.path.join(arguments.control, "control.in")).decode("utf-8")
    control = (template.replace("@VERSION@", arguments.version)
               .replace("@ARCHITECTURE@", ARCHITECTURE)
               .replace("@INSTALLED_SIZE@", str(installed_size)))
    if "@" in control:
        sys.exit("в control.in остались незаменённые поля @...@")
    package = re.search(r"^Package:\s*(\S+)", control, re.MULTILINE).group(1)
    control_entries = [("dir", "./", None, 0), ("file", "./control", control.encode("utf-8"), 0o644)]
    for name in sorted(os.listdir(arguments.control)):
        if name in CONTROL_SCRIPTS:
            control_entries.append(("file", "./" + name, read_text_file(os.path.join(arguments.control, name)), 0o755))
    outer = [
        ("file", "./debian-binary", b"2.0\n", 0o644),
        ("file", "./data.tar.gz", tar_gz(data), 0o644),
        ("file", "./control.tar.gz", tar_gz(control_entries), 0o644),
    ]
    os.makedirs(arguments.out, exist_ok=True)
    path = os.path.join(arguments.out, f"{package}_{arguments.version}_{ARCHITECTURE}.ipk")
    with open(path, "wb") as file:
        file.write(tar_gz(outer))
    print(path)


def read_control(ipk_path: str) -> str:
    with tarfile.open(ipk_path, mode="r:gz") as outer:
        member = next(m for m in outer.getmembers() if m.name.lstrip("./") == "control.tar.gz")
        control_archive = outer.extractfile(member).read()
    with tarfile.open(fileobj=io.BytesIO(control_archive), mode="r:gz") as inner:
        member = next(m for m in inner.getmembers() if m.name.lstrip("./") == "control")
        return inner.extractfile(member).read().decode("utf-8")


def version_key(file_name: str):
    match = re.search(r"_(\d+)\.(\d+)\.(\d+)(?:-(\d+))?_", file_name)
    return tuple(int(part or 0) for part in match.groups()) if match else (0, 0, 0, 0)


def index(arguments) -> None:
    packages = sorted((name for name in os.listdir(arguments.feed) if name.endswith(".ipk")), key=version_key)
    blocks = []
    for name in packages:
        path = os.path.join(arguments.feed, name)
        data = open(path, "rb").read()
        control = read_control(path).rstrip("\n")
        blocks.append(f"{control}\nFilename: {name}\nSize: {len(data)}\nSHA256sum: {hashlib.sha256(data).hexdigest()}\n")
    text = "\n".join(blocks).encode("utf-8")
    with open(os.path.join(arguments.feed, "Packages"), "wb") as file:
        file.write(text)
    with open(os.path.join(arguments.feed, "Packages.gz"), "wb") as file:
        file.write(gzip_bytes(text))
    print(f"Packages: {len(packages)}")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    commands = parser.add_subparsers(dest="command", required=True)
    build_parser = commands.add_parser("build")
    build_parser.add_argument("--control", required=True)
    build_parser.add_argument("--data", required=True)
    build_parser.add_argument("--version", required=True)
    build_parser.add_argument("--out", required=True)
    index_parser = commands.add_parser("index")
    index_parser.add_argument("--feed", required=True)
    arguments = parser.parse_args()
    build(arguments) if arguments.command == "build" else index(arguments)


if __name__ == "__main__":
    main()
