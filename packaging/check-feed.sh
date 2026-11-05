#!/usr/bin/env bash
# Проверяет feed после сборки: состав ipk, права, поля control и SHA256sum в индексе.
#   packaging/check-feed.sh <каталог feed> <версия>
set -euo pipefail

feed_dir=$1
version=$2
ipk="$feed_dir/flowgate_${version}_aarch64-3.10.ipk"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

fail() {
    echo "check-feed: $*" >&2
    exit 1
}

[ -f "$ipk" ] || fail "нет $ipk"
[ "$(tar -tzf "$ipk" | sort | tr '\n' ' ')" = "./control.tar.gz ./data.tar.gz ./debian-binary " ] \
    || fail "неверный состав ipk: $(tar -tzf "$ipk" | tr '\n' ' ')"

tar -xzf "$ipk" -C "$work"
[ "$(cat "$work/debian-binary")" = "2.0" ] || fail "debian-binary не 2.0"

mkdir "$work/control" "$work/data"
tar -xzf "$work/control.tar.gz" -C "$work/control"
tar -xzf "$work/data.tar.gz" -C "$work/data"

grep -qx "Version: $version" "$work/control/control" || fail "в control нет Version: $version"
grep -qx "Architecture: aarch64-3.10" "$work/control/control" || fail "в control нет Architecture: aarch64-3.10"
executable() {
    tar -tvzf "$1" | awk -v name="./$2" '$NF == name && $1 == "-rwxr-xr-x" { found = 1 } END { exit !found }'
}
for script in preinst postinst prerm postrm; do
    executable "$work/control.tar.gz" "$script" || fail "$script отсутствует или не 755"
done
for file in opt/bin/flowgate opt/etc/init.d/S99flowgate opt/share/flowgate/upgrade.sh; do
    executable "$work/data.tar.gz" "$file" || fail "$file отсутствует или не 755"
done
[ -f "$work/data/opt/share/flowgate/appsettings.json" ] || fail "нет appsettings.json"
[ -d "$work/data/opt/share/flowgate/www" ] || fail "нет каталога www"
if grep -rlq $'\r' "$work/control" "$work/data/opt/etc" "$work/data/opt/share/flowgate/upgrade.sh"; then
    fail "скрипты с CRLF"
fi

expected=$(sha256sum "$ipk" | cut -d' ' -f1)
grep -q "^SHA256sum: $expected$" "$feed_dir/Packages" || fail "SHA256sum в Packages не совпадает"
[ "$(gzip -dc "$feed_dir/Packages.gz" | sha256sum)" = "$(sha256sum < "$feed_dir/Packages")" ] || fail "Packages.gz не совпадает с Packages"

echo "check-feed: $ipk в порядке"
