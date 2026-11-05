#!/usr/bin/env bash
# Собирает flowgate_<версия>_aarch64-3.10.ipk и обновляет индекс feed.
#   packaging/build-ipk.sh <версия> <каталог сборки: flowgate + appsettings.json> <каталог фронта или -> <каталог feed>
# В каталоге feed уже могут лежать прежние ipk: индекс Packages описывает все.
set -euo pipefail

if [ "$#" -ne 4 ]; then
    echo "usage: $0 <version> <router-build-dir> <www-dir|-> <feed-dir>" >&2
    exit 2
fi

version=$1
build_dir=$2
www_dir=$3
feed_dir=$4
here=$(cd "$(dirname "$0")" && pwd)
python=${PYTHON:-$(command -v python3 || command -v python)}

for file in flowgate appsettings.json; do
    [ -f "$build_dir/$file" ] || { echo "нет $build_dir/$file" >&2; exit 1; }
done

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT

mkdir -p "$stage/opt/bin" "$stage/opt/etc/init.d" "$stage/opt/share/flowgate/www"
cp "$build_dir/flowgate" "$stage/opt/bin/flowgate"
cp "$build_dir/appsettings.json" "$stage/opt/share/flowgate/appsettings.json"
cp "$here/files/S99flowgate" "$stage/opt/etc/init.d/S99flowgate"
cp "$here/files/upgrade.sh" "$stage/opt/share/flowgate/upgrade.sh"
cp "$here/files/nginx.conf" "$stage/opt/share/flowgate/nginx.conf"
if [ "$www_dir" != "-" ]; then
    cp -R "$www_dir/." "$stage/opt/share/flowgate/www/"
fi

mkdir -p "$feed_dir"
"$python" "$here/ipk.py" build --control "$here/control" --data "$stage" --version "$version" --out "$feed_dir"
"$python" "$here/ipk.py" index --feed "$feed_dir"
