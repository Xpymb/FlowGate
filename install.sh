#!/bin/sh
# Установка FlowGate на роутер Keenetic с Entware (aarch64):
#   opkg update && opkg install wget-ssl ca-bundle
#   /opt/bin/wget -qO- https://xpymb.github.io/FlowGate/install.sh | sh
set -eu

# opkg качает feed через wget из PATH: wget-ssl (/opt/bin) должен идти раньше BusyBox (/opt/usr/bin)
export PATH=/opt/sbin:/opt/bin:/opt/usr/sbin:/opt/usr/bin:/usr/sbin:/usr/bin:/sbin:/bin

FEED_URL=${FLOWGATE_FEED_URL:-https://xpymb.github.io/FlowGate/aarch64-3.10}
FEED_FILE=/opt/etc/opkg/flowgate.conf

fail() {
    echo "FlowGate: $*" >&2
    exit 1
}

command -v opkg >/dev/null 2>&1 || fail "не найден opkg: нужен Entware (https://help.keenetic.com/hc/ru/articles/360021214160)"
case "$(uname -m)" in
    aarch64|arm64) ;;
    *) fail "поддерживается только aarch64, у роутера $(uname -m)" ;;
esac

echo "FlowGate: ставлю wget-ssl и ca-bundle (opkg скачивает feed по HTTPS)"
opkg update >/dev/null || fail "opkg update не выполнен"
opkg install wget-ssl ca-bundle || fail "не удалось установить wget-ssl и ca-bundle"

mkdir -p /opt/etc/opkg
printf 'src/gz flowgate %s\n' "$FEED_URL" >"$FEED_FILE.new"
mv -f "$FEED_FILE.new" "$FEED_FILE"
echo "FlowGate: подключён feed $FEED_URL"

opkg update || { rm -f "$FEED_FILE"; fail "opkg update с feed FlowGate не выполнен, feed отключён"; }
opkg install flowgate || fail "opkg install flowgate не выполнен"

address=$(ip -4 addr show br0 2>/dev/null | sed -n 's/.*inet \([0-9.]*\).*/\1/p' | head -n 1)
echo "FlowGate установлен: http://${address:-<адрес роутера>}:8090"
