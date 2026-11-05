using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Dal.Implementations.DomainSources;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Dal.Implementations.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.Tests.Common.Processes;
using FlowGate.Tests.Unit.Connections;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.UpdateProxy;

/// <summary>
///     Тесты <see cref="UpdateProxyService"/>: запуск по требованию, аренды, остановка сразу после последней, перезапуск
///     при смене доменов, пути <see cref="UpdateVia"/>, причины недоступности
/// </summary>
public sealed class UpdateProxyServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UnixEpoch);
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeProcessLauncher _launcher = new();
    private readonly XrayProcessManager _processes;
    private readonly SettingsProvider _settings;
    private readonly SubscriptionProvider _subscriptions;
    private readonly UpdateProxyService _sut;
    private readonly int _port;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyServiceTests"/>
    /// </summary>
    public UpdateProxyServiceTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "xray"), "fake");
        _listener.Start();
        _port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        var storeFactory = new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = Path.Combine(_directory, "data") }));
        _settings = new SettingsProvider(storeFactory);
        var servers = new ServerProvider(storeFactory);
        _subscriptions = new SubscriptionProvider(storeFactory);
        servers.AddAsync([ConnectionTestData.Server()], CancellationToken.None).GetAwaiter().GetResult();

        var xrayOptions = MsOptions.Create(new XrayOptions { BinaryPath = Path.Combine(_directory, "xray"), WorkDirectory = Path.Combine(_directory, "work") });
        _processes = new XrayProcessManager(_launcher, new FakeProcessTable(), xrayOptions, NullLogger<XrayProcessManager>.Instance);
        _sut = new UpdateProxyService(
            _settings,
            servers,
            _subscriptions,
            new DomainSourceProvider(storeFactory),
            new XrayConfigBuilder(),
            _runner,
            _processes,
            xrayOptions,
            MsOptions.Create(new UpdateProxyOptions { Port = _port, UpgradePort = _port + 1 }),
            _time,
            NullLogger<UpdateProxyService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string ConfigPath => Path.Combine(_directory, "work", UpdateProxyService.ConfigFileName);

    /// <summary>
    ///     Прокси выключен — в режиме Auto аренды нет, Xray не запускается
    /// </summary>
    [Fact]
    public async Task AcquireAsync_AutoDisabled_ReturnsNullWithoutStarting()
    {
        var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(lease);
        Assert.Empty(_launcher.Started);
        Assert.Equal(UpdateProxyState.Disabled, status.State);
    }

    /// <summary>
    ///     Direct — без прокси, даже если он включён
    /// </summary>
    [Fact]
    public async Task AcquireAsync_Direct_ReturnsNull()
    {
        await EnableAsync();

        Assert.Null(await _sut.AcquireAsync(UpdateVia.Direct, Token));
        Assert.Empty(_launcher.Started);
    }

    /// <summary>
    ///     Proxy — через выбранный сервер, даже если прокси выключен в настройках
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ProxyWhileDisabled_StartsWithSelectedServer()
    {
        await EnableAsync(enabled: false);

        using var lease = await _sut.AcquireAsync(UpdateVia.Proxy, Token);

        Assert.NotNull(lease);
        Assert.Single(_launcher.Started);
    }

    /// <summary>
    ///     Proxy, а прокси недоступен — ошибка, а не тихий переход на прямую загрузку
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ProxyUnavailable_Throws()
    {
        await EnableAsync("srv-gone");

        var ex = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => _sut.AcquireAsync(UpdateVia.Proxy, Token));

        Assert.Contains("srv-gone", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Первая аренда запускает экземпляр через менеджер процессов, вторая берёт тот же процесс
    /// </summary>
    [Fact]
    public async Task AcquireAsync_Enabled_StartsOnceAndSharesProcess()
    {
        await EnableAsync();

        using var first = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        using var second = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.NotNull(first);
        Assert.Equal(new Uri($"http://127.0.0.1:{_port}/"), first.ProxyUri);
        var process = Assert.Single(_launcher.Started);
        Assert.Equal(Path.Combine(_directory, "xray"), process.Request.FileName);
        Assert.Equal(["run", "-c", ConfigPath], process.Request.Arguments);
        var test = Assert.Single(_runner.Calls, call => call.Arguments is ["run", "-test", "-c", _]);
        Assert.EndsWith(".json", test.Arguments[3], StringComparison.Ordinal);
        var config = await File.ReadAllTextAsync(ConfigPath, Token);
        Assert.Contains("out-update-proxy", config, StringComparison.Ordinal);
        Assert.Contains("domain:github.com", config, StringComparison.Ordinal);
        Assert.Equal(UpdateProxyState.Running, status.State);
        Assert.Equal(2, status.ActiveRequests);
    }

    /// <summary>
    ///     Экземпляр останавливается сразу после последней аренды, а не раньше
    /// </summary>
    [Fact]
    public async Task Release_LastLease_StopsImmediately()
    {
        await EnableAsync();
        var first = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        var second = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        var process = Assert.Single(_launcher.Started);

        first!.Dispose();
        await Task.Delay(50, Token);
        Assert.False(process.WasKilled);

        second!.Dispose();
        await WaitUntilAsync(() => process.WasKilled);
        Assert.Equal(UpdateProxyState.Idle, (await _sut.GetStatusAsync(Token)).State);
    }

    /// <summary>
    ///     Добавилась подписка: пока идут запросы, работает старый конфиг; следующая операция получает новый
    /// </summary>
    [Fact]
    public async Task AcquireAsync_DomainsChanged_NewConfigForNextOperation()
    {
        await EnableAsync();
        var first = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        await _subscriptions.AddAsync(Subscription("https://new-provider.example.com/token"), Token);

        var second = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        Assert.Single(_launcher.Started);

        first!.Dispose();
        second!.Dispose();
        await WaitUntilAsync(() => _launcher.Started[0].WasKilled);
        using var third = await _sut.AcquireAsync(UpdateVia.Auto, Token);

        Assert.Equal(2, _launcher.Started.Count);
        Assert.Contains("full:new-provider.example.com", await File.ReadAllTextAsync(ConfigPath, Token), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Сервер пропал — аренды нет, причина видна в состоянии
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ServerMissing_ReturnsNullWithReason()
    {
        await EnableAsync("srv-gone");

        var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(lease);
        Assert.Empty(_launcher.Started);
        Assert.Equal(UpdateProxyState.Unavailable, status.State);
        Assert.Contains("srv-gone", status.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Xray не установлен — запросы идут напрямую
    /// </summary>
    [Fact]
    public async Task AcquireAsync_XrayNotInstalled_ReturnsNull()
    {
        await EnableAsync();
        File.Delete(Path.Combine(_directory, "xray"));

        var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);

        Assert.Null(lease);
        Assert.Equal("Xray не установлен", (await _sut.GetStatusAsync(Token)).Reason);
    }

    /// <summary>
    ///     Xray отклонил конфигурацию — процесс не запускается, конфиг не подменяется
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ConfigRejected_ReturnsNullWithoutStarting()
    {
        await EnableAsync();
        _runner.Handler = (_, _) => new ProcessResult(1, string.Empty, "Failed to build outbound > invalid password");

        var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);

        Assert.Null(lease);
        Assert.Empty(_launcher.Started);
        Assert.False(File.Exists(ConfigPath));
        Assert.Contains("invalid password", (await _sut.GetStatusAsync(Token)).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Процесс завершился сразу после запуска — аренды нет, в причине код выхода
    /// </summary>
    [Fact]
    public async Task AcquireAsync_ProcessExitsImmediately_ReturnsNull()
    {
        await EnableAsync();
        _launcher.ExitOnStart = _ => 23;

        var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);

        Assert.Null(lease);
        Assert.Contains("23", (await _sut.GetStatusAsync(Token)).Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Экземпляр упал посреди операции — следующая аренда поднимает его снова
    /// </summary>
    [Fact]
    public async Task AcquireAsync_AfterCrash_StartsAgain()
    {
        await EnableAsync();
        using var running = await _sut.AcquireAsync(UpdateVia.Auto, Token);
        _launcher.Started[0].Exit(2);
        await WaitUntilAsync(async () => (await _sut.GetStatusAsync(Token)).State == UpdateProxyState.Unavailable);

        using var lease = await _sut.AcquireAsync(UpdateVia.Auto, Token);

        Assert.NotNull(lease);
        Assert.Equal(2, _launcher.Started.Count);
    }

    /// <summary>
    ///     Конфигурация для скрипта самообновления — на отдельном порту, проверена Xray
    /// </summary>
    [Fact]
    public async Task PrepareStandaloneAsync_Enabled_WritesConfigForUpgradePort()
    {
        await EnableAsync();

        var standalone = await _sut.PrepareStandaloneAsync(Token);

        Assert.NotNull(standalone);
        Assert.Equal(_port + 1, standalone.Port);
        Assert.Equal(Path.Combine(_directory, "xray"), standalone.BinaryPath);
        Assert.Contains($"\"port\": {_port + 1}", await File.ReadAllTextAsync(standalone.ConfigPath, Token), StringComparison.Ordinal);
        Assert.Empty(_launcher.Started);
    }

    /// <summary>
    ///     Прокси выключен — скрипт самообновления идёт напрямую
    /// </summary>
    [Fact]
    public async Task PrepareStandaloneAsync_Disabled_ReturnsNull()
    {
        Assert.Null(await _sut.PrepareStandaloneAsync(Token));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _processes.Dispose();
        _listener.Stop();
        _listener.Dispose();
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task EnableAsync(string serverId = "srv-de", bool enabled = true) =>
        await _settings.UpdateAsync(
            settings => settings with { UpdateProxy = new UpdateProxySettings { Enabled = enabled, ServerId = serverId } },
            Token);

    private static Subscription Subscription(string url) =>
        new("sub1", null, url, true, null, null, false, Guid.NewGuid().ToString(), false, null, null, DateTimeOffset.UnixEpoch);

    private static Task WaitUntilAsync(Func<bool> condition) => WaitUntilAsync(() => Task.FromResult(condition()));

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        for (var attempt = 0; attempt < 100 && !await condition(); attempt++)
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.True(await condition());
    }
}
