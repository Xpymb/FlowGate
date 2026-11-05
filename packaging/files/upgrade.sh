#!/bin/sh
# Самообновление FlowGate: запускается из FlowGate отвязанным процессом, opkg останавливает и запускает службу сам.
# Итог — последней строкой в журнале: "ok <версия>" или "failed <причина>".

PATH=/opt/sbin:/opt/bin:/opt/usr/sbin:/opt/usr/bin:/usr/sbin:/usr/bin:/sbin:/bin
COPY=/tmp/flowgate-upgrade.sh
LOCK=/tmp/flowgate-upgrade.lock
LOG=${FLOWGATE_UPGRADE_LOG:-/tmp/flowgate/upgrade.log}
INIT=/opt/etc/init.d/S99flowgate

if [ "$0" != "$COPY" ]; then
    cp -f "$0" "$COPY" && chmod 755 "$COPY" && exec /bin/sh "$COPY" "$@"
    exit 1
fi

mkdir -p "$(dirname "$LOG")"
exec >>"$LOG" 2>&1

if ! mkdir "$LOCK" 2>/dev/null; then
    echo "failed обновление уже идёт"
    exit 1
fi
trap 'rm -rf "$LOCK" "$COPY" /tmp/flowgate-upgrade.pid' EXIT

installed_version() {
    opkg status flowgate | sed -n 's/^Version: //p'
}

echo "$(date '+%Y-%m-%d %H:%M:%S') обновление FlowGate, установлена $(installed_version)"
before=$(installed_version)

if ! opkg update; then
    echo "failed opkg update не выполнен"
    exit 1
fi

if ! opkg upgrade flowgate; then
    "$INIT" status >/dev/null || "$INIT" start
    echo "failed opkg upgrade flowgate не выполнен"
    exit 1
fi

"$INIT" status >/dev/null || "$INIT" start
after=$(installed_version)
if [ "$before" = "$after" ]; then
    echo "ok $after (уже последняя)"
else
    echo "ok $after"
fi
