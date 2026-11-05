#!/bin/sh
# Самообновление FlowGate: запускается из FlowGate отвязанным процессом, opkg останавливает и запускает службу сам.
# Итог — последней строкой в журнале: "ok <версия>" или "failed <причина>".
# С аргументами --xray <бинарник> --proxy-config <файл> --proxy-port <порт> пакет качается через прокси обновлений:
# отдельный xray запускается здесь же, потому что служба FlowGate (и её xray) на время opkg останавливается.

PATH=/opt/sbin:/opt/bin:/opt/usr/sbin:/opt/usr/bin:/usr/sbin:/usr/bin:/sbin:/bin
COPY=/tmp/flowgate-upgrade.sh
LOCK=/tmp/flowgate-upgrade.lock
LOG=${FLOWGATE_UPGRADE_LOG:-/tmp/flowgate/upgrade.log}
INIT=/opt/etc/init.d/S99flowgate
PROXY_PID=/tmp/flowgate-upgrade-proxy.pid
XRAY=
PROXY_CONFIG=
PROXY_PORT=

if [ "$0" != "$COPY" ]; then
    cp -f "$0" "$COPY" && chmod 755 "$COPY" && exec /bin/sh "$COPY" "$@"
    exit 1
fi

while [ $# -gt 0 ]; do
    case "$1" in
        --xray) XRAY=$2; shift 2 ;;
        --proxy-config) PROXY_CONFIG=$2; shift 2 ;;
        --proxy-port) PROXY_PORT=$2; shift 2 ;;
        *) shift ;;
    esac
done

mkdir -p "$(dirname "$LOG")"
exec >>"$LOG" 2>&1

if ! mkdir "$LOCK" 2>/dev/null; then
    echo "failed обновление уже идёт"
    exit 1
fi

stop_proxy() {
    if [ -f "$PROXY_PID" ]; then
        start-stop-daemon -K -q -p "$PROXY_PID" >/dev/null 2>&1
        rm -f "$PROXY_PID"
    fi
    [ -n "$PROXY_CONFIG" ] && rm -f "$PROXY_CONFIG"
}

trap 'stop_proxy; rm -rf "$LOCK" "$COPY" /tmp/flowgate-upgrade.pid' EXIT

proxy_alive() {
    pid=$(cat "$PROXY_PID" 2>/dev/null)
    [ -n "$pid" ] && kill -0 "$pid" 2>/dev/null
}

start_proxy() {
    [ -n "$XRAY" ] && [ -n "$PROXY_CONFIG" ] && [ -n "$PROXY_PORT" ] || return 1
    [ -x "$XRAY" ] && [ -f "$PROXY_CONFIG" ] || return 1

    start-stop-daemon -S -b -m -p "$PROXY_PID" -x "$XRAY" -- run -c "$PROXY_CONFIG" || return 1

    i=0
    while [ $i -lt 10 ]; do
        sleep 1
        proxy_alive || return 1
        if netstat -ltn 2>/dev/null | grep -q "127.0.0.1:$PROXY_PORT "; then
            return 0
        fi
        i=$((i + 1))
    done
    proxy_alive
}

installed_version() {
    opkg status flowgate | sed -n 's/^Version: //p'
}

echo "$(date '+%Y-%m-%d %H:%M:%S') обновление FlowGate, установлена $(installed_version)"
before=$(installed_version)

if [ -n "$PROXY_CONFIG" ]; then
    if start_proxy; then
        export http_proxy="http://127.0.0.1:$PROXY_PORT"
        export https_proxy="$http_proxy"
        echo "загрузка через прокси обновлений (127.0.0.1:$PROXY_PORT)"
    else
        stop_proxy
        echo "прокси обновлений не запустился, загрузка напрямую"
    fi
fi

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
