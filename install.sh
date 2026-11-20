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

command -v opkg >/dev/null 2>&1 || fail "opkg not found: Entware is required (https://help.keenetic.com/hc/en-us/articles/360021214160)"
case "$(uname -m)" in
    aarch64|arm64) ;;
    *) fail "only aarch64 is supported, router is $(uname -m)" ;;
esac

echo "FlowGate: installing wget-ssl and ca-bundle (opkg fetches the feed over HTTPS)"
opkg update >/dev/null || fail "opkg update failed"
opkg install wget-ssl ca-bundle || fail "failed to install wget-ssl and ca-bundle"

mkdir -p /opt/etc/opkg
printf 'src/gz flowgate %s\n' "$FEED_URL" >"$FEED_FILE.new"
mv -f "$FEED_FILE.new" "$FEED_FILE"
echo "FlowGate: feed added $FEED_URL"

opkg update || { rm -f "$FEED_FILE"; fail "opkg update with the FlowGate feed failed, feed removed"; }
opkg install flowgate || fail "opkg install flowgate failed"

address=$(ip -4 addr show br0 2>/dev/null | sed -n 's/.*inet \([0-9.]*\).*/\1/p' | head -n 1)
echo "FlowGate installed: http://${address:-<router address>}:8090"
