# CLAUDE.md

Руководство для Claude Code по бэкенду FlowGate.

FlowGate — менеджер Xray для роутеров Keenetic (aarch64, KeeneticOS 5): WebUI, установка и обновление Xray,
подписки, соединения socks/http/wireguard как интерфейсы Keenetic, штатные DNS-маршруты KeeneticOS со списками
доменов v2fly. Бэкенд — один NativeAOT-бинарник на роутере. Фронт живёт отдельно (`src/frontend`), бэкенд отдаёт
его статику и REST API.

Факты о роутере, проверенные на железе (команды NDMS, коды ошибок RCI, лимиты, память), — в
[`docs/keenetic-findings.md`](../../docs/keenetic-findings.md). Перед работой с NDMS читай его.

## Команды

> ⚠️ **`dotnet build` запускать запрещено.** Для проверки компиляции используй `dotnet test` — он сам пересоберёт
> нужные проекты. Интеграционные тесты ссылаются на `FlowGate.WebApi`, поэтому хост тоже компилируется.

`dotnet test` работает в режиме **Microsoft.Testing.Platform** (`global.json` рядом с `FlowGate.slnx`), поэтому
команды запускай из `src/backend` или глубже.

```bash
# все тесты
dotnet test --solution FlowGate.slnx

# один проект
dotnet test --project Tests/FlowGate.Tests.Unit/FlowGate.Tests.Unit.csproj

# фильтр по классу / методу (маски *)
dotnet test --project Tests/FlowGate.Tests.Unit/FlowGate.Tests.Unit.csproj --filter-class "*KeeneticRciClientTests"

# покрытие (Microsoft.Testing.Extensions.CodeCoverage), отчёт в TestResults/
dotnet test --solution FlowGate.slnx --coverage --coverage-output-format cobertura
```

Синтаксис VSTest (`--filter "FullyQualifiedName~..."`, путь проекта без `--project`) в MTP не работает.

Бинарник для роутера собирается **только** кросс-сборкой в Docker на x64 → `linux-musl-arm64`, статически, вместе
с OpenSSL. Сборка под эмуляцией arm64 не годится: ILCompiler под QEMU падает случайным образом.

```bash
# результат: out/router/flowgate (static-pie aarch64, ~20 МиБ) + appsettings.json
docker buildx build -f docker/Dockerfile.router --build-arg VERSION=0.1.0 --output type=local,dest=out/router .

# пакет и индекс feed (из корня репозитория; нужны bash и python3)
packaging/build-ipk.sh 0.1.0 src/backend/out/router - out/feed
```

Проверка на роутере (только `/tmp`, конфигурацию Keenetic не меняет; `scp` только с `-O`, `nohup` на роутере нет):

```bash
scp -O -P 222 out/router/* root@192.168.1.1:/tmp/fgtest/
ssh -p 222 root@192.168.1.1 'cd /tmp/fgtest && chmod +x flowgate && start-stop-daemon -S -b -m -p pid -d . -x /bin/sh -- -c "exec ./flowgate --urls http://192.168.1.1:8090 --FlowGate:DataDirectory=/tmp/fgtest/data --Xray:BinaryPath=/tmp/fgtest/xray --Xray:WorkDirectory=/tmp/fgtest/work >>flowgate.log 2>&1"'
# остановка (FlowGate сам остановит xray): start-stop-daemon -K -s TERM -p /tmp/fgtest/pid
```

Локальный запуск (`dotnet run`, профиль `http`, `http://localhost:6000`) ходит в RCI через SSH-туннель:
`ssh -p 222 -L 7979:127.0.0.1:79 root@192.168.1.1` (см. `appsettings.Development.json`).

## Архитектура

### Проекты

```
Services/
  FlowGate.WebApi                  хост: Program.cs, Endpoints/<Фича>/, Jobs/, Serialization/, Hosting/
  FlowGate.SharedContracts         Tier 1: DTO API (Dto/, Requests/) — без логики
  FlowGate.Contracts               Tier 2: доменные сущности с логикой (<Фича>/)
  Bll/FlowGate.Bll.Interfaces      интерфейсы сервисов (<Фича>/I*Service)
  Bll/FlowGate.Bll.Implementations реализации + маппинг DTO <-> Entity (<Фича>/)
  Dal/FlowGate.Dal.Interfaces      интерфейсы провайдеров хранения (I*Provider)
  Dal/FlowGate.Dal.Implementations провайдеры (JSON-файлы через Shared.Dal)
Shared/
  FlowGate.Exceptions              исключения API с errorCode и HTTP-статусом
  FlowGate.Shared.Options          классы опций, их валидаторы и регистрация
  FlowGate.Shared.Bll              общая BLL-инфраструктура (очередь операций NDMS)
  FlowGate.Shared.Dal              JsonFileStore<T>: атомарная запись JSON
  FlowGate.Shared.WebApi           ExceptionHandlerMiddleware, формат ошибок
  Keenetic/FlowGate.Shared.Keenetic.HttpClient   RCI-клиент Keenetic
  GitHub/FlowGate.Shared.GitHub.HttpClient       релизы и скачивание файлов с GitHub
Tests/
  FlowGate.Tests.Common            фикстуры (embedded), FakeHttpMessageHandler
  FlowGate.Tests.Unit
  FlowGate.Tests.Integration       WebApplicationFactory<Program>; ссылается на WebApi намеренно
```

### Поток зависимостей

```
WebApi ──► SharedContracts, Bll.Interfaces, Shared.WebApi, Shared.Options
Bll.Implementations ──► Bll.Interfaces, Contracts, SharedContracts, Dal.Interfaces, Shared.Bll, Keenetic.HttpClient
Contracts ──► SharedContracts
Dal.Implementations ──► Dal.Interfaces, Contracts, Shared.Dal
Shared.* ──► Exceptions, Shared.Options (не на Services/*)
```

- Эндпоинты работают только с DTO из `SharedContracts`; маппинг DTO ↔ Entity — в BLL.
- В `SharedContracts` нет логики. Бизнес-правила — в сущностях `Contracts` и сервисах BLL.
- Все записи в NDMS идут через `INdmsOperationQueue` (один писатель). Параллельные записи в конфигурацию Keenetic
  дают гонки — BROray на этом обжёгся.

### NativeAOT — обязательные правила

- MVC-контроллеров нет: Minimal API, класс `XxxEndpoints` со статическим `MapXxxEndpoints(this IEndpointRouteBuilder)`.
- JSON только через source-generated `JsonSerializerContext`. Новый DTO API → добавить в
  `WebApi/Serialization/AppJsonSerializerContext.cs`. Никакого `JsonSerializer.Serialize<T>(value)` без `JsonTypeInfo`.
- Без рефлексии: никакого Mapster/AutoMapper, `Activator.CreateInstance`, сканирования сборок. Маппинг — руками
  (статические `*Mapper.ToDto()`).
- Опции: `Bind` (генератор привязки конфигурации включён в `Directory.Build.props`) + валидатор через
  `[OptionsValidator]`.
- У библиотек включён `IsAotCompatible` — предупреждения IL2xxx/IL3xxx не игнорировать.

### Xray

- Xray — **дочерний процесс** FlowGate (`IXrayProcessService`): автозапуск, перезапуск после падения с паузой
  1→2→4…→60 с, pid-файл в `Xray:WorkDirectory`, при старте убивается осиротевший xray (если `/proc/<pid>/exe` — наш бинарник).
- Любая операция, которая трогает бинарник или конфиг при работающем процессе, идёт через
  `IXrayProcessService.RunExclusiveAsync`, иначе супервизор может перезапустить xray посреди подмены.
- `xray.json` не пишется руками: модель (`IXrayConfigSource`) → `IXrayConfigBuilder` → кандидат → `xray run -test` → атомарная
  замена (`IXrayConfigService`). При старте FlowGate конфиг пересобирается.
- Установка (`IXrayInstaller`) — фоновая задача `xray-install`: скачать zip + `.dgst`, сверить SHA-256, взять из архива только
  `xray`, проверить `version` и текущий конфиг новым бинарником, подменить (`.new` → rename, прежний в `.bak`), при неудачном
  запуске вернуть `.bak`.

### Серверы и подписки

- Сервер = имя + **outbound Xray как есть** (`JsonObject`, без `tag`); форм нет — UI правит JSON, проверку делает
  установленный Xray (`IOutboundValidator`: временный конфиг с одним outbound → `xray run -test`, ошибки сокращаются до
  двух последних звеньев цепочки). `JsonObject` изменяемый: из `IServerProvider` всегда выходят копии (`Server.DeepCopy()`).
- Парсеры ссылок (`Servers/Parsing`) выдают плоский `settings` Xray 26; Xray-JSON подписок может быть с `vnext`/`servers` —
  `OutboundSummary` понимает обе формы, поэтому `ServerImportKey` (без учётных данных) одинаков для ссылки и JSON.
- Синхронизация подписки — чистая функция `SubscriptionSync.Apply`: ID `sub-<subId>-<key[0..12]>` стабилен при ротации
  пароля и смене формата; если в ответе есть отклонённые узлы, пропавшие серверы сохраняются (`Partial`); 0 принятых —
  `Failed`, каталог не трогается.
- URL подписки содержит токен: в логи и ошибки — только хост, в репозиторий и фикстуры — никогда.

### Соединения

- Соединение = inbound Xray `in-<id>` на `Connections:ListenAddress:<порт>` + outbound `out-<id>` (копия сервера) +
  интерфейс Keenetic (`ProxyN` для socks/http, `WireguardN` для wg). Модель Xray собирает `ConnectionXrayModel`;
  сервера нет (ушёл из подписки) — правило на `block`, трафик напрямую не уходит.
- Команды NDMS — только из `KeeneticInterfaceCommands`, через `IKeeneticInterfaceService.ExecuteAsync` (очередь + save).
  Свой интерфейс узнаётся по описанию `FlowGate: <имя>`; чужие не трогаем.
- `up` отправляется только при создании интерфейса. Выключенный пользователем интерфейс сверка не включает: чинит только
  пропавшие (создаёт заново) и разошедшиеся поля (`FindDrift`).
- Ключи WG — `xray wg` (нужен установленный Xray). Порт, имя интерфейса и адрес WG выбирает `ConnectionAllocator`.
- Создание откатывается целиком: Xray отклонил — соединение не сохраняется; Keenetic отклонил — `no interface`, удаление
  записи, пересборка Xray. Сервер, выбранный в соединении, удалить нельзя (409).

### Источники списков и DNS-маршруты

- Источник (`DomainSource`): GitHub-репозиторий v2fly (`data/`, tarball коммита), geosite.dat / geoip.dat (релиз или
  прямая ссылка), локальный. Все виды читаются через `IListSourceReader` и дают одно и то же: каталог и `ListContent`
  (домены, IPv4, IPv6 вперемешку — Keenetic держит их в одной группе). Формат для пользователя —
  [`docs/domain-source-format.md`](../../docs/domain-source-format.md).
- Файлы источников — `<DataDirectory>/sources/<id>.data` и кеш каталога `<id>.catalog.json`; новая версия качается в
  кандидата, проверяется построением каталога и только потом заменяет файл.
- Маршрут = один список → одно соединение. Применение в Keenetic — одна фоновая задача `dns-routes-apply`, план строит
  чистая функция `DnsRoutePlanner` по состоянию из `GET /rci/object-group/fqdn` и `/rci/dns-proxy/route`.
- Правила пользователя (не нарушать): правило включается только при создании; выключенное пользователем не включаем;
  удалённое пользователем правило возвращаем выключенным; удалённая основная группа — маршрут удаляется и из FlowGate;
  правила на пересозданный сверкой интерфейс возвращаются с прежним флагом. Состав групп меняется только по разнице.
- На KeeneticOS 5.2 удаление группы **не** удаляет её правила (снимаем явно), удаление интерфейса — удаляет.

### Фоновые задачи

Долгие операции — через `IJobRunner` (в памяти, одна задача вида одновременно, иначе `ConflictException`, история из 20).
Эндпоинт отвечает **202** + `JobDto` + `Location: /api/jobs/{id}`, прогресс — шаги `IJobContext.Report`.

### Хранение

JSON-файлы в `FlowGate:DataDirectory` (на роутере `/opt/etc/flowgate/`) через `JsonFileStore<T>`: запись во
временный файл + rename, на Linux права 600. БД нет.

### Ошибки

`ExceptionHandlerMiddleware` отвечает `{errorCode, message, traceId}`:

| Исключение | HTTP | errorCode |
|---|---|---|
| `EntityNotFoundException` | 404 | `ENTITY_NOT_FOUND` |
| `RequestValidationException` | 400 | `VALIDATION_ERROR` |
| `ConflictException` | 409 | `CONFLICT` |
| `XrayOperationException` | 500 | `XRAY_OPERATION_FAILED` |
| `KeeneticCommandException` | 502 | `KEENETIC_COMMAND_FAILED` |
| `KeeneticUnavailableException` | 503 | `KEENETIC_UNAVAILABLE` |
| `ExternalServiceUnavailableException` | 503 | `EXTERNAL_UNAVAILABLE` |
| `BadHttpRequestException` | его статус | `BAD_REQUEST` |
| прочее | 500 | `INTERNAL_ERROR` |

4xx логируются Warning одной строкой без стека, 5xx — Error с исключением.
Бизнес-события — одна строка на событие (`Xray обновлён 26.9.9 → 26.10.1`).

### RCI Keenetic

- Чтение: `IKeeneticRciClient.GetAsync("show/version")` → `GET http://127.0.0.1:79/rci/show/version`.
- Запись: `ExecuteAsync(commands)` → `POST /rci/` с `[{"parse":"<cli>"}]`. HTTP всегда 200, результат каждой
  команды в `status[]`. **Пакет не транзакционный**: ошибка посередине не останавливает остальные команды —
  проверяй `RciBatchResult` (`EnsureSuccess`, `Failed`).
- Сохранение: `SaveConfigurationAsync()` (`system configuration save`) — один раз в конце операции.
- Коды ошибок — `RciErrorCodes`. Удаление несуществующего объекта даёт `7405602 argument parse error`.

### Упаковка и самообновление

- Пакет `flowgate_<версия>_aarch64-3.10.ipk` (tar.gz, как в Entware) собирает `packaging/build-ipk.sh` через
  `packaging/ipk.py` (режимы и владелец задаются явно, сборка воспроизводима); `packaging/check-feed.sh` проверяет
  результат. Скрипты пакета — только LF (`.gitattributes`), сборщик отклоняет CRLF.
- Раскладка: `/opt/bin/flowgate`, `/opt/share/flowgate/{appsettings.json,www/,upgrade.sh}` (рабочий каталог службы),
  `/opt/etc/init.d/S99flowgate`. Данные `/opt/etc/flowgate` и Xray `/opt/sbin/flowgate-xray` в пакет не входят и
  при `upgrade`/`remove` не трогаются. Журнал службы — `/tmp/flowgate/flowgate.log`.
- opkg вызывает `prerm remove` и при `--force-reinstall`, поэтому `flowgate cleanup` (`ICleanupService`) не теряет
  настройки: удаляет из Keenetic свои интерфейсы, группы `fg-*` и правила, а маршрутам ставит `Provisioned=false` с
  прежним `LastKnownDisabled` — после установки сверка создаёт всё заново с теми же флагами.
- Самообновление (`ISystemService.RequestUpgrade`, задача `system-upgrade`): `start-stop-daemon -S -b` запускает
  `upgrade.sh` отвязанным процессом (переживает остановку службы), тот копирует себя в `/tmp` и выполняет
  `opkg update && opkg upgrade flowgate`; итог — последняя строка `ok …`/`failed …` в `System:UpgradeLogPath`. Отката нет.
- Релиз: тег `vX.Y.Z` → `.github/workflows/release.yml` (тесты, `Dockerfile.router` с `VERSION`, фронт при наличии
  `src/frontend/package.json`, ipk, GitHub Release, feed `gh-pages/aarch64-3.10` с тремя последними версиями).

## Code Conventions

Перенесены из LessQuick с одним отличием: **copyright-заголовков в `.cs` нет**.

### XML-документация

- Документируй всё публичное: классы, интерфейсы, методы, свойства, **конструкторы**. На русском, `///`.
- Конструктор: `Инициализирует новый экземпляр класса <see cref="ClassName"/>`.
- Interface → Implementation: полная документация в интерфейсе, `/// <inheritdoc />` в реализации.
- `<param>`, `<returns>`, `<exception>`, `<see cref=""/>` по месту.
- **Никаких `//`-комментариев внутри методов** (если пользователь не попросил). Код объясняет себя именами.

### Стиль

- Проверки на null — `is null` / `is not null`, не `== null`.
- Аргументы конструктора: `_x = x ?? throw new ArgumentNullException(nameof(x));`.
- File-scoped namespaces, скобки Allman, блоки `case` в фигурных скобках.
- Именование: интерфейсы с `I`, `*Service` (BLL), `*Provider` (DAL), `*Job` (фоновые задачи), `*Endpoints`
  (Minimal API); приватные поля `_camelCase`; асинхронные методы с суффиксом `Async`.
- Порядок членов: public → protected → private. Using: System → сторонние → проектные; лишние удалять.
- LINQ вместо циклов там, где читается проще.
- `async`/`await` для любого I/O, `CancellationToken` прокидывается до конца.

### Фоновые задачи

`BackgroundService` + `PeriodicTimer` в `WebApi/Jobs/`. Джоба только тикает: создаёт scope, берёт сервис BLL и
делегирует ему. Логики в джобе нет.

## Тесты

- Стек: xunit.v3 (MTP), NSubstitute, `NullLogger<T>`, `Microsoft.Testing.Extensions.CodeCoverage`.
- Именование: `Method_Scenario_ExpectedResult`.
- Структура: подмены в полях через `Substitute.For<T>()`, `_sut` создаётся в конструкторе.
- Без `//`-комментариев в телах тестов; публичные тест-классы и `[Fact]`/`[Theory]` — с краткой XML-документацией.
- Фикстуры — embedded resources в `Tests.Common/Fixtures/` (ответы RCI сняты с реального роутера).
- HTTP к Keenetic и GitHub подменяется `FakeHttpMessageHandler` (`FakeXrayGitHub` — релизы Xray с генерируемыми zip/.dgst),
  а не моком `HttpClient`.
- Keenetic в тестах — `FakeKeeneticRci` (`Tests.Common/Keenetic`): модель интерфейсов, групп и DNS-правил с поведением
  роутера, запоминает CLI-команды, `FailWhen` для ошибок; в интеграционных тестах доступен как `factory.Keenetic`.
- Источники в тестах: `DatFileBuilder` собирает geosite/geoip.dat, `RepositoryArchive` — tarball GitHub;
  `FakeXrayGitHub.Extra` отвечает на коммиты и tarball, `factory.DomainSourceHttp` — на прямые ссылки.
- Процессы подменяются `FakeProcessRunner`/`FakeProcessLauncher`: версия фейкового xray записана в сам файл (`FakeXrayBinary`),
  поэтому установка, подмена и откат проверяются на настоящих файлах во временном каталоге.
- Таймеры и паузы — через внедрённый `TimeProvider`; в тестах `FakeTimeProvider`.
- Мокать: RCI-клиент, внешние HTTP, провайдеры DAL в unit-тестах. Не мокать: доменные сущности, парсеры,
  `JsonFileStore` (пишет во временный каталог).
