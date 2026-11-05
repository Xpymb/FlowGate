# FlowGate

Менеджер Xray для роутеров Keenetic с WebUI: установка и обновление Xray, подписки, соединения socks/http/wireguard
как интерфейсы Keenetic, штатные DNS-маршруты KeeneticOS по спискам доменов v2fly.

Требования: KeeneticOS 5, архитектура **aarch64**, установленный [Entware](https://help.keenetic.com/hc/ru/articles/360021214160).

## Установка

В SSH-консоли Entware:

```sh
opkg update && opkg install wget-ssl ca-bundle
/opt/bin/wget -qO- https://xpymb.github.io/FlowGate/install.sh | sh
```

Встроенный `wget` (BusyBox) не умеет HTTPS, поэтому первой командой ставится `wget-ssl` (`/opt/bin/wget`); opkg
скачивает через него и feed FlowGate. Скрипт подключает feed
`https://xpymb.github.io/FlowGate/aarch64-3.10` и ставит пакет `flowgate`. WebUI — `http://<адрес роутера>:8090`.

## Обновление

Кнопкой в WebUI (FlowGate раз в сутки проверяет новые релизы) или вручную:

```sh
opkg update && opkg upgrade flowgate
```

Настройки в `/opt/etc/flowgate` при обновлении не меняются.

## Служба

```sh
/opt/etc/init.d/S99flowgate start|stop|restart|status
```

Журнал — `/tmp/flowgate/flowgate.log` (в памяти, после перезагрузки начинается заново).
Свои настройки поверх стандартных — `/opt/etc/flowgate/appsettings.json`.

## Удаление

```sh
opkg remove flowgate
```

Перед удалением FlowGate убирает из конфигурации Keenetic свои интерфейсы, группы `fg-*` и DNS-маршруты; чужие
объекты не трогает. Настройки и Xray остаются: повторная установка вернёт соединения и маршруты. Удалить всё:

```sh
opkg remove flowgate
rm -rf /opt/etc/flowgate /opt/sbin/flowgate-xray* /opt/etc/opkg/flowgate.conf
```

Посмотреть, что будет удалено из Keenetic, не меняя ничего: `cd /opt/share/flowgate && flowgate cleanup --dry-run`.

## Разработка

Бэкенд — `src/backend` (см. [`src/backend/CLAUDE.md`](src/backend/CLAUDE.md)), упаковка — `packaging/`, CI —
`.github/workflows/`. Релиз выпускается тегом `vX.Y.Z`: тесты → NativeAOT-сборка → ipk → GitHub Release → feed в
ветке `gh-pages`.
