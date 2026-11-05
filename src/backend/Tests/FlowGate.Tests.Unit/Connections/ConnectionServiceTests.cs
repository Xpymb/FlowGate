using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using FlowGate.Bll.Implementations.Connections;
using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Dal.Implementations.Connections;
using FlowGate.Dal.Implementations.DnsRoutes;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Connections;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Keenetic;
using FlowGate.Tests.Common.Processes;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="ConnectionService"/> с настоящими хранилищами во временном каталоге и подменённым Keenetic
/// </summary>
public sealed class ConnectionServiceTests : IDisposable
{
    private const string SaveCommand = "system configuration save";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeKeeneticRci _keenetic = new();
    private readonly IXrayConfigService _xrayConfig = Substitute.For<IXrayConfigService>();
    private readonly IXrayVersionProvider _versionProvider = Substitute.For<IXrayVersionProvider>();
    private readonly NdmsOperationQueue _queue;
    private readonly ServerProvider _servers;
    private readonly ConnectionProvider _connections;
    private readonly DnsRouteProvider _routes;
    private readonly ConnectionService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionServiceTests"/>
    /// </summary>
    public ConnectionServiceTests()
    {
        var storeFactory = new JsonFileStoreFactory(Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }));
        _servers = new ServerProvider(storeFactory);
        _connections = new ConnectionProvider(storeFactory);
        _routes = new DnsRouteProvider(storeFactory);
        _queue = new NdmsOperationQueue(Microsoft.Extensions.Options.Options.Create(new KeeneticOptions()), NullLogger<NdmsOperationQueue>.Instance);
        _versionProvider.IsInstalled.Returns(true);

        var rci = new KeeneticRciClient(new HttpClient(_keenetic.Handler) { BaseAddress = new Uri("http://127.0.0.1:79/rci/") });
        var interfaces = new KeeneticInterfaceService(rci, _queue, NullLogger<KeeneticInterfaceService>.Instance);
        var keyGenerator = new XrayWireguardKeyGenerator(new FakeProcessRunner(), _versionProvider, Microsoft.Extensions.Options.Options.Create(new XrayOptions()));

        _sut = new ConnectionService(
            _connections,
            _servers,
            _routes,
            interfaces,
            keyGenerator,
            _xrayConfig,
            Microsoft.Extensions.Options.Options.Create(new ConnectionOptions()),
            TimeProvider.System,
            NullLogger<ConnectionService>.Instance);

        _servers.AddAsync([ConnectionTestData.Server()], CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Socks-соединение: первый порт, Proxy0, конфигурация Xray применена, интерфейс создан, включён и сохранён
    /// </summary>
    [Fact]
    public async Task CreateAsync_Socks_CreatesProxyInterfaceAndAppliesXray()
    {
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("YouTube", "socks", "srv-de"), Token);

        Assert.StartsWith("con-", created.Id);
        Assert.Equal("Socks", created.Kind);
        Assert.Equal(10800, created.Port);
        Assert.Equal("Proxy0", created.InterfaceName);
        Assert.Equal("Германия", created.ServerName);
        Assert.False(created.SupportsUdp);
        Assert.Equal("up", created.Interface!.State);
        Assert.Empty(created.Problems);
        Assert.Contains("interface Proxy0 proxy upstream 127.0.0.1 10800", _keenetic.Commands);
        Assert.Contains("interface Proxy0 up", _keenetic.Commands);
        Assert.Equal(SaveCommand, _keenetic.Commands[^1]);
        await _xrayConfig.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Занятые в Keenetic имена пропускаются, порты не повторяются
    /// </summary>
    [Fact]
    public async Task CreateAsync_ProxyNameTakenInKeenetic_UsesNextNameAndPort()
    {
        _keenetic.AddInterface("Proxy0", RciFixtures.ShowInterfaceProxy);

        var first = await _sut.CreateAsync(new ConnectionCreateRequest("A", "Socks", "srv-de"), Token);
        var second = await _sut.CreateAsync(new ConnectionCreateRequest("B", "Http", "srv-de"), Token);

        Assert.Equal(("Proxy1", 10800), (first.InterfaceName, first.Port));
        Assert.Equal(("Proxy2", 10801), (second.InterfaceName, second.Port));
        Assert.Contains("interface Proxy2 proxy protocol http", _keenetic.Commands);
    }

    /// <summary>
    ///     WireGuard-соединение получает ключи, адрес 10.255.0.2 и peer на свой порт
    /// </summary>
    [Fact]
    public async Task CreateAsync_Wireguard_GeneratesKeysAndPeer()
    {
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("Игры", "wireguard", "srv-de"), Token);
        var stored = await _connections.GetAsync(created.Id, Token);

        Assert.Equal("Wireguard0", created.InterfaceName);
        Assert.True(created.SupportsUdp);
        Assert.Equal("10.255.0.2", stored!.Wireguard!.ClientAddress);
        Assert.NotEqual(stored.Wireguard.ServerPublicKey, stored.Wireguard.ClientPublicKey);
        Assert.Contains($"interface Wireguard0 wireguard peer {stored.Wireguard.ServerPublicKey} endpoint 127.0.0.1:10800", _keenetic.Commands);
        Assert.Contains($"interface Wireguard0 wireguard private-key {stored.Wireguard.ClientPrivateKey}", _keenetic.Commands);
    }

    /// <summary>
    ///     Xray отклонил конфигурацию — соединение не сохраняется, Keenetic не трогается
    /// </summary>
    [Fact]
    public async Task CreateAsync_XrayRejects_SavesNothing()
    {
        _xrayConfig.ApplyAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new XrayOperationException("bad config"));

        await Assert.ThrowsAsync<XrayOperationException>(() => _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token));

        Assert.Empty(await _connections.GetAllAsync(Token));
        Assert.Empty(_keenetic.Commands);
    }

    /// <summary>
    ///     Keenetic отклонил команду — интерфейс удаляется, соединение не сохраняется, конфигурация Xray пересобирается
    /// </summary>
    [Fact]
    public async Task CreateAsync_KeeneticRejects_RollsBack()
    {
        _keenetic.FailWhen = command => command.EndsWith("proxy connect", StringComparison.Ordinal);

        await Assert.ThrowsAsync<KeeneticCommandException>(() => _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token));

        Assert.Empty(await _connections.GetAllAsync(Token));
        Assert.Empty(_keenetic.InterfaceNames);
        Assert.Contains("no interface Proxy0", _keenetic.Commands);
        await _xrayConfig.Received(2).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Некорректные запросы отклоняются до любых изменений
    /// </summary>
    /// <param name="name">Имя</param>
    /// <param name="kind">Вид</param>
    /// <param name="serverId">Сервер</param>
    [Theory]
    [InlineData("", "socks", "srv-de")]
    [InlineData("Имя \"в кавычках\"", "socks", "srv-de")]
    [InlineData("A", "vmess", "srv-de")]
    [InlineData("A", "socks", "srv-missing")]
    public async Task CreateAsync_InvalidRequest_ThrowsValidation(string name, string kind, string serverId)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(new ConnectionCreateRequest(name, kind, serverId), Token));

        Assert.Empty(_keenetic.Commands);
    }

    /// <summary>
    ///     Имя соединения уникально без учёта регистра
    /// </summary>
    [Fact]
    public async Task CreateAsync_DuplicateName_ThrowsValidation()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("YouTube", "socks", "srv-de"), Token);

        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(new ConnectionCreateRequest("youtube", "http", "srv-de"), Token));
    }

    /// <summary>
    ///     Переименование меняет только описание интерфейса; смена сервера пересобирает конфигурацию Xray
    /// </summary>
    [Fact]
    public async Task UpdateAsync_RenameAndChangeServer_UpdatesDescriptionAndXray()
    {
        await _servers.AddAsync([ConnectionTestData.Server("srv-nl")], Token);
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.ClearCommands();
        _xrayConfig.ClearReceivedCalls();

        var updated = await _sut.UpdateAsync(created.Id, new ConnectionUpdateRequest("B", "srv-nl"), Token);

        Assert.Equal(("B", "srv-nl"), (updated.Name, updated.ServerId));
        Assert.Equal(["interface Proxy0 description \"FlowGate: B\"", SaveCommand], _keenetic.Commands);
        await _xrayConfig.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Xray отклонил новый сервер — соединение остаётся прежним
    /// </summary>
    [Fact]
    public async Task UpdateAsync_XrayRejectsNewServer_KeepsOldServer()
    {
        await _servers.AddAsync([ConnectionTestData.Server("srv-nl")], Token);
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _xrayConfig.ApplyAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new XrayOperationException("bad"));

        await Assert.ThrowsAsync<XrayOperationException>(() => _sut.UpdateAsync(created.Id, new ConnectionUpdateRequest("A", "srv-nl"), Token));

        Assert.Equal("srv-de", (await _connections.GetAsync(created.Id, Token))!.ServerId);
    }

    /// <summary>
    ///     Удаление убирает интерфейс из Keenetic и соединение из хранилища
    /// </summary>
    [Fact]
    public async Task DeleteAsync_Existing_RemovesInterfaceAndConnection()
    {
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);

        await _sut.DeleteAsync(created.Id, Token);

        Assert.Empty(_keenetic.InterfaceNames);
        Assert.Empty(await _connections.GetAllAsync(Token));
        Assert.Equal(["no interface Proxy0", SaveCommand], _keenetic.Commands.TakeLast(2));
    }

    /// <summary>
    ///     Соединение с назначенным DNS-маршрутом удалить нельзя — 409, Keenetic не трогается
    /// </summary>
    [Fact]
    public async Task DeleteAsync_UsedByRoute_ThrowsConflict()
    {
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        var now = DateTimeOffset.UnixEpoch;
        await _routes.AddAsync(new DnsRoute("rt-1", "v2fly", "youtube", null, created.Id, "fg-youtube", false, null, false, null, null, now, now), Token);
        _keenetic.ClearCommands();

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(created.Id, Token));

        Assert.Contains("«youtube»", exception.Message);
        Assert.Empty(_keenetic.Commands);
    }

    /// <summary>
    ///     Интерфейс уже удалён пользователем — удаление соединения всё равно проходит
    /// </summary>
    [Fact]
    public async Task DeleteAsync_InterfaceAlreadyMissing_Succeeds()
    {
        var created = await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.RemoveInterface("Proxy0");

        await _sut.DeleteAsync(created.Id, Token);

        Assert.Empty(await _connections.GetAllAsync(Token));
    }

    /// <summary>
    ///     Удалённый в Keenetic интерфейс создаётся заново и включается
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_InterfaceMissing_RecreatesWithUp()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.RemoveInterface("Proxy0");
        _keenetic.ClearCommands();

        var repaired = await _sut.ReconcileAsync(Token);

        Assert.Equal(1, repaired.Repaired);
        Assert.Equal(["Proxy0"], repaired.RecreatedInterfaces);
        Assert.Contains("interface Proxy0 up", _keenetic.Commands);
        Assert.Equal("FlowGate: A", _keenetic.GetInterface("Proxy0")!["description"]!.GetValue<string>());
    }

    /// <summary>
    ///     Выключенный пользователем интерфейс сверка не включает и ничего не отправляет
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_InterfaceDisabledByUser_SendsNothing()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.SetInterfaceField("Proxy0", "state", "down");
        _keenetic.ClearCommands();

        var repaired = await _sut.ReconcileAsync(Token);

        Assert.Equal(0, repaired.Repaired);
        Assert.Empty(_keenetic.Commands);
        Assert.Equal("down", _keenetic.GetInterface("Proxy0")!["state"]!.GetValue<string>());
    }

    /// <summary>
    ///     Разошедшееся описание исправляется без включения интерфейса
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_DescriptionDrift_RepairsWithoutUp()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.SetInterfaceField("Proxy0", "description", "FlowGate: старое имя");
        _keenetic.ClearCommands();

        var repaired = await _sut.ReconcileAsync(Token);

        Assert.Equal(1, repaired.Repaired);
        Assert.Contains("interface Proxy0 description \"FlowGate: A\"", _keenetic.Commands);
        Assert.DoesNotContain("interface Proxy0 up", _keenetic.Commands);
    }

    /// <summary>
    ///     Интерфейс с тем же именем, но чужим описанием не трогается
    /// </summary>
    [Fact]
    public async Task ReconcileAsync_ForeignInterface_LeavesUntouched()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        _keenetic.SetInterfaceField("Proxy0", "description", "VPN провайдера");
        _keenetic.ClearCommands();

        var repaired = await _sut.ReconcileAsync(Token);
        var listed = Assert.Single(await _sut.GetAllAsync(Token));

        Assert.Equal(0, repaired.Repaired);
        Assert.Empty(_keenetic.Commands);
        Assert.False(listed.Interface!.Owned);
        Assert.Contains(listed.Problems, problem => problem.Contains("не принадлежит FlowGate"));
    }

    /// <summary>
    ///     Сервер соединения удалён (ушёл из подписки) — в списке видна проблема
    /// </summary>
    [Fact]
    public async Task GetAllAsync_ServerDeleted_ReportsProblem()
    {
        await _sut.CreateAsync(new ConnectionCreateRequest("A", "socks", "srv-de"), Token);
        await _servers.DeleteAsync("srv-de", Token);

        var listed = Assert.Single(await _sut.GetAllAsync(Token));

        Assert.Null(listed.ServerName);
        Assert.Contains(listed.Problems, problem => problem.Contains("Сервер удалён"));
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
}
