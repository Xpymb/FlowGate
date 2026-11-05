using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.DnsRoutes;
using FlowGate.Bll.Implementations.DomainSources;
using FlowGate.Bll.Implementations.DomainSources.Readers;
using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Implementations.Maintenance;
using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Connections;
using FlowGate.Dal.Implementations.Connections;
using FlowGate.Dal.Implementations.DnsRoutes;
using FlowGate.Dal.Implementations.DomainSources;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.DnsRoutes;
using FlowGate.SharedContracts.Dto.DomainSources;
using FlowGate.Tests.Common.Keenetic;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.DnsRoutes;

/// <summary>
///     Тесты <see cref="DnsRouteService"/> и <see cref="DomainSourceService"/> вместе: локальный источник, настоящие
///     хранилища во временном каталоге, подменный Keenetic с моделью групп и правил
/// </summary>
public sealed class DnsRouteServiceTests : IDisposable
{
    private const string Group = "fg-telegram";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeKeeneticRci _keenetic = new();
    private readonly NdmsOperationQueue _queue;
    private readonly JobRunner _jobs = new(TimeProvider.System, NullLogger<JobRunner>.Instance);
    private readonly ConnectionProvider _connections;
    private readonly DnsRouteProvider _routes;
    private readonly DnsRouteService _sut;
    private readonly DomainSourceService _sources;
    private readonly KeeneticDnsService _dns;
    private readonly KeeneticRciClient _rci;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DnsRouteServiceTests"/>
    /// </summary>
    public DnsRouteServiceTests()
    {
        var flowGateOptions = MsOptions.Create(new FlowGateOptions { DataDirectory = _directory });
        var storeFactory = new JsonFileStoreFactory(flowGateOptions);
        var sourceOptions = MsOptions.Create(new DomainSourceOptions());
        _connections = new ConnectionProvider(storeFactory);
        _routes = new DnsRouteProvider(storeFactory);
        var sourceProvider = new DomainSourceProvider(storeFactory);
        var localLists = new LocalListProvider(storeFactory);
        var files = new DomainSourceFileProvider(flowGateOptions);
        var reader = new DomainListReader(
            [new GeositeDatReader(), new GeoipDatReader(), new GitHubRepositoryReader(sourceOptions), new LocalSourceReader(localLists)],
            files);

        _queue = new NdmsOperationQueue(MsOptions.Create(new KeeneticOptions()), NullLogger<NdmsOperationQueue>.Instance);
        _rci = new KeeneticRciClient(new HttpClient(_keenetic.Handler) { BaseAddress = new Uri("http://127.0.0.1:79/rci/") });
        _dns = new KeeneticDnsService(_rci, _queue, sourceOptions, NullLogger<KeeneticDnsService>.Instance);

        _sut = new DnsRouteService(
            _routes,
            sourceProvider,
            _connections,
            reader,
            _dns,
            _jobs,
            TimeProvider.System,
            MsOptions.Create(new DomainSourceOptions { MaxEntriesPerRoute = 5 }),
            MsOptions.Create(new KeeneticOptions()),
            NullLogger<DnsRouteService>.Instance);
        _sources = new DomainSourceService(
            sourceProvider,
            localLists,
            files,
            reader,
            Substitute.For<IDomainSourceDownloader>(),
            _routes,
            _sut,
            _jobs,
            TimeProvider.System,
            new SettingsProvider(storeFactory),
            Substitute.For<IUpdateProxyService>(),
            sourceOptions,
            NullLogger<DomainSourceService>.Instance);

        var now = DateTimeOffset.UnixEpoch;
        _connections.AddAsync(new Connection("con-wg", "Игры", ConnectionKind.Wireguard, "srv", 10801, "Wireguard0", null, now, now), CancellationToken.None).GetAwaiter().GetResult();
        _connections.AddAsync(new Connection("con-socks", "Видео", ConnectionKind.Socks, "srv", 10800, "Proxy0", null, now, now), CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Локальный список → маршрут → в Keenetic группа с доменами и подсетями и включённое правило
    /// </summary>
    [Fact]
    public async Task Create_LocalList_AppliesEnabledRule()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n91.108.56.1/22\n");

        var route = await CreateRouteAsync(sourceId, "con-wg");

        Assert.Equal(["telegram.org", "91.108.56.0/22"], _keenetic.Groups[Group]);
        Assert.Equal("FlowGate: telegram (Мои списки)", _keenetic.GroupDescription(Group));
        Assert.Equal(new FakeDnsRoute(Group, "Wireguard0", true, false), Assert.Single(_keenetic.Routes));
        Assert.Equal("Enabled", (await _sut.GetAsync(route.Id, Token)).State);
    }

    /// <summary>
    ///     Выключенное пользователем правило не включается после изменения списка; повторная сверка ничего не шлёт
    /// </summary>
    [Fact]
    public async Task ListChanged_RuleDisabledByUser_StaysDisabled()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        var route = await CreateRouteAsync(sourceId, "con-wg");
        _keenetic.SetRouteDisabled(Group, "Wireguard0", disabled: true);

        await _sources.SaveLocalListAsync(sourceId, "telegram", new LocalListSaveRequest("telegram.org\nt.me\n"), Token);
        await WaitApplyAsync();
        _keenetic.ClearCommands();
        var second = await _sut.ReconcileAsync(new HashSet<string>(), Token);

        Assert.Equal(["telegram.org", "t.me"], _keenetic.Groups[Group]);
        Assert.True(Assert.Single(_keenetic.Routes).Disabled);
        Assert.Equal("Disabled", (await _sut.GetAsync(route.Id, Token)).State);
        Assert.Equal(0, second);
        Assert.Empty(_keenetic.Commands);
    }

    /// <summary>
    ///     Правило удалено пользователем — сверка возвращает его выключенным
    /// </summary>
    [Fact]
    public async Task Reconcile_RuleDeletedByUser_RestoresDisabled()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        await CreateRouteAsync(sourceId, "con-wg");
        _keenetic.RemoveRoute(Group, "Wireguard0");

        await _sut.ReconcileAsync(new HashSet<string>(), Token);

        Assert.True(Assert.Single(_keenetic.Routes).Disabled);
    }

    /// <summary>
    ///     Интерфейс пересоздан (правила ушли вместе с ним) — правило возвращается включённым, как было
    /// </summary>
    [Fact]
    public async Task Reconcile_InterfaceRecreated_RestoresEnabled()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        await CreateRouteAsync(sourceId, "con-wg");
        _keenetic.RemoveRoute(Group, "Wireguard0");

        await _sut.ReconcileAsync(new HashSet<string>(["Wireguard0"], StringComparer.OrdinalIgnoreCase), Token);

        Assert.False(Assert.Single(_keenetic.Routes).Disabled);
    }

    /// <summary>
    ///     Список (группа) удалён пользователем — маршрут удаляется из FlowGate, висящее правило снимается
    /// </summary>
    [Fact]
    public async Task Reconcile_GroupDeletedByUser_RemovesRoute()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        await CreateRouteAsync(sourceId, "con-wg");
        _keenetic.RemoveGroup(Group);

        await _sut.ReconcileAsync(new HashSet<string>(), Token);

        Assert.Empty(await _routes.GetAllAsync(Token));
        Assert.Empty(_keenetic.Routes);
    }

    /// <summary>
    ///     Явное выключение и включение из FlowGate, затем смена соединения с переносом флага
    /// </summary>
    [Fact]
    public async Task SetDisabledAndMove_UpdatesRule()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        var route = await CreateRouteAsync(sourceId, "con-wg");

        await _sut.SetDisabledAsync(route.Id, disabled: true, Token);
        await WaitApplyAsync();
        var disabled = Assert.Single(_keenetic.Routes);
        await _sut.UpdateAsync(route.Id, new DnsRouteUpdateRequest("con-socks"), Token);
        await WaitApplyAsync();
        var moved = Assert.Single(_keenetic.Routes);
        await _sut.SetDisabledAsync(route.Id, disabled: false, Token);
        await WaitApplyAsync();

        Assert.True(disabled.Disabled);
        Assert.Equal(new FakeDnsRoute(Group, "Proxy0", true, true), moved);
        Assert.False(Assert.Single(_keenetic.Routes).Disabled);
    }

    /// <summary>
    ///     Удаление маршрута убирает группу и правило из Keenetic
    /// </summary>
    [Fact]
    public async Task Delete_RemovesGroupAndRule()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        var route = await CreateRouteAsync(sourceId, "con-wg");

        await _sut.DeleteAsync(route.Id, Token);
        await WaitApplyAsync();

        Assert.Empty(_keenetic.Groups);
        Assert.Empty(_keenetic.Routes);
    }

    /// <summary>
    ///     Отказы при создании: нет списка, повтор, список больше лимита, нет соединения
    /// </summary>
    [Fact]
    public async Task Create_Invalid_Rejected()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        await _sources.SaveLocalListAsync(sourceId, "big", new LocalListSaveRequest("a.com\nb.com\nc.com\nd.com\ne.com\nf.com\n"), Token);
        await CreateRouteAsync(sourceId, "con-wg");

        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(new DnsRouteCreateRequest(sourceId, "missing", "con-wg"), Token));
        await Assert.ThrowsAsync<ConflictException>(() => _sut.CreateAsync(new DnsRouteCreateRequest(sourceId, "telegram", "con-socks"), Token));
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(new DnsRouteCreateRequest(sourceId, "big", "con-wg"), Token));
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(new DnsRouteCreateRequest(sourceId, "big", "con-missing"), Token));
    }

    /// <summary>
    ///     Локальный список с ошибкой не сохраняется: 400 с номерами строк; используемый список не удаляется
    /// </summary>
    [Fact]
    public async Task LocalList_InvalidOrUsed_Rejected()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        await CreateRouteAsync(sourceId, "con-wg");

        var invalid = await Assert.ThrowsAsync<RequestValidationException>(
            () => _sources.SaveLocalListAsync(sourceId, "other", new LocalListSaveRequest("ok.com\nпример.рф\n"), Token));
        await Assert.ThrowsAsync<ConflictException>(() => _sources.DeleteLocalListAsync(sourceId, "telegram", Token));
        await Assert.ThrowsAsync<ConflictException>(() => _sources.DeleteAsync(sourceId, Token));

        Assert.StartsWith("строка 2", Assert.Single(invalid.Details!));
    }

    /// <summary>
    ///     Переустановка пакета: <c>flowgate cleanup</c> убирает группы и правила, сверка возвращает маршрут с прежним
    ///     флагом выключенности и не удаляет его из FlowGate
    /// </summary>
    [Fact]
    public async Task Cleanup_ThenReconcile_RestoresRouteWithPreviousFlag()
    {
        var sourceId = await CreateLocalSourceAsync("telegram.org\n");
        var route = await CreateRouteAsync(sourceId, "con-wg");
        _keenetic.SetRouteDisabled(Group, "Wireguard0", disabled: true);
        await _sut.ReconcileAsync(new HashSet<string>(), Token);
        var cleanup = new CleanupService(
            Substitute.For<IXrayProcessManager>(),
            _dns,
            new KeeneticInterfaceService(_rci, _queue, NullLogger<KeeneticInterfaceService>.Instance),
            _routes,
            NullLogger<CleanupService>.Instance);

        await cleanup.RunAsync(dryRun: false, Token);
        Assert.Empty(_keenetic.Groups);
        Assert.Empty(_keenetic.Routes);

        await _sut.ReconcileAsync(new HashSet<string>(), Token);

        Assert.Equal(["telegram.org"], _keenetic.Groups[Group]);
        Assert.Equal(new FakeDnsRoute(Group, "Wireguard0", true, true), Assert.Single(_keenetic.Routes));
        Assert.Equal("Disabled", (await _sut.GetAsync(route.Id, Token)).State);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sut.Dispose();
        _queue.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<string> CreateLocalSourceAsync(string telegram)
    {
        var source = await _sources.CreateAsync(new DomainSourceSaveRequest("Мои списки", "Local"), Token);
        await _sources.SaveLocalListAsync(source.Id, "telegram", new LocalListSaveRequest(telegram), Token);
        return source.Id;
    }

    private async Task<DnsRouteDto> CreateRouteAsync(string sourceId, string connectionId)
    {
        var route = await _sut.CreateAsync(new DnsRouteCreateRequest(sourceId, "telegram", connectionId), Token);
        await WaitApplyAsync();
        return route;
    }

    private async Task WaitApplyAsync()
    {
        foreach (var job in _jobs.List().Where(job => job.Kind == DnsRouteService.ApplyJobKind))
        {
            var finished = await _jobs.WaitAsync(job.Id, Token);
            Assert.Null(finished?.Error);
        }
    }
}
