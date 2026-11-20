using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Settings;
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
///     Тесты <see cref="UpdateProxyService"/>: выбор пути <see cref="UpdateVia"/> по настройкам и состоянию основного Xray,
///     причины недоступности, конфигурация для скрипта самообновления
/// </summary>
public sealed class UpdateProxyServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly FakeProcessRunner _runner = new();
    private readonly IXrayProcessService _xray = Substitute.For<IXrayProcessService>();
    private readonly SettingsProvider _settings;
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
        SetXrayState(XrayProcessState.Running);

        var storeFactory = new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = Path.Combine(_directory, "data") }));
        _settings = new SettingsProvider(storeFactory);
        var servers = new ServerProvider(storeFactory);
        servers.AddAsync([ConnectionTestData.Server()], CancellationToken.None).GetAwaiter().GetResult();

        _sut = new UpdateProxyService(
            _settings,
            servers,
            _xray,
            new XrayConfigBuilder(),
            _runner,
            MsOptions.Create(new XrayOptions { BinaryPath = Path.Combine(_directory, "xray"), WorkDirectory = Path.Combine(_directory, "work") }),
            MsOptions.Create(new UpdateProxyOptions { Port = _port, UpgradePort = _port + 1 }),
            NullLogger<UpdateProxyService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Прокси выключен — в режиме Auto напрямую
    /// </summary>
    [Fact]
    public async Task ResolveAsync_AutoDisabled_ReturnsNull()
    {
        await EnableAsync(enabled: false);

        var proxy = await _sut.ResolveAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(proxy);
        Assert.Equal(UpdateProxyState.Disabled, status.State);
    }

    /// <summary>
    ///     Direct — без прокси, даже если он включён
    /// </summary>
    [Fact]
    public async Task ResolveAsync_Direct_ReturnsNull()
    {
        await EnableAsync();

        Assert.Null(await _sut.ResolveAsync(UpdateVia.Direct, Token));
    }

    /// <summary>
    ///     Включён, Xray запущен и слушает порт — адрес inbound основного Xray
    /// </summary>
    [Fact]
    public async Task ResolveAsync_EnabledAndRunning_ReturnsInboundUri()
    {
        await EnableAsync();

        var proxy = await _sut.ResolveAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Equal(new Uri($"http://127.0.0.1:{_port}/"), proxy);
        Assert.Equal(UpdateProxyState.Ready, status.State);
        Assert.Null(status.Reason);
    }

    /// <summary>
    ///     Proxy — через выбранный сервер, даже если прокси выключен в настройках
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ProxyWhileDisabled_ReturnsInboundUri()
    {
        await EnableAsync(enabled: false);

        Assert.NotNull(await _sut.ResolveAsync(UpdateVia.Proxy, Token));
    }

    /// <summary>
    ///     Proxy, а прокси недоступен — ошибка, а не тихий переход на прямую загрузку
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ProxyUnavailable_Throws()
    {
        await EnableAsync("srv-gone");

        var ex = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => _sut.ResolveAsync(UpdateVia.Proxy, Token));

        Assert.Contains("srv-gone", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Сервер пропал — напрямую, причина видна в состоянии
    /// </summary>
    [Fact]
    public async Task ResolveAsync_ServerMissing_ReturnsNullWithReason()
    {
        await EnableAsync("srv-gone");

        var proxy = await _sut.ResolveAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(proxy);
        Assert.Equal(UpdateProxyState.Unavailable, status.State);
        Assert.Contains("srv-gone", status.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Основной Xray остановлен — прокси недоступен, сам Xray не запускается
    /// </summary>
    [Theory]
    [InlineData(XrayProcessState.Stopped, "Xray не запущен")]
    [InlineData(XrayProcessState.Restarting, "Xray не запущен")]
    [InlineData(XrayProcessState.NotInstalled, "Xray не установлен")]
    public async Task ResolveAsync_XrayNotRunning_ReturnsNullWithReason(XrayProcessState state, string reason)
    {
        await EnableAsync();
        SetXrayState(state);

        var proxy = await _sut.ResolveAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(proxy);
        Assert.Equal(UpdateProxyState.Unavailable, status.State);
        Assert.Equal(reason, status.Reason);
        await _xray.DidNotReceive().StartAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Xray запущен, но порт не слушается (конфиг ещё не применён) — напрямую с причиной
    /// </summary>
    [Fact]
    public async Task ResolveAsync_PortClosed_ReturnsNullWithReason()
    {
        await EnableAsync();
        _listener.Stop();

        var proxy = await _sut.ResolveAsync(UpdateVia.Auto, Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Null(proxy);
        Assert.Contains(_port.ToString(System.Globalization.CultureInfo.InvariantCulture), status.Reason, StringComparison.Ordinal);
    }

    /// <summary>
    ///     После успешного запроса прежняя причина сбрасывается
    /// </summary>
    [Fact]
    public async Task ResolveAsync_RecoveredAfterFailure_ClearsReason()
    {
        await EnableAsync();
        SetXrayState(XrayProcessState.Stopped);
        await _sut.ResolveAsync(UpdateVia.Auto, Token);
        SetXrayState(XrayProcessState.Running);

        await _sut.ResolveAsync(UpdateVia.Auto, Token);

        Assert.Null((await _sut.GetStatusAsync(Token)).Reason);
    }

    /// <summary>
    ///     Конфигурация для скрипта самообновления — на отдельном порту, весь трафик через сервер, проверена Xray
    /// </summary>
    [Fact]
    public async Task PrepareStandaloneAsync_Enabled_WritesConfigForUpgradePort()
    {
        await EnableAsync();

        var standalone = await _sut.PrepareStandaloneAsync(Token);

        Assert.NotNull(standalone);
        Assert.Equal(_port + 1, standalone.Port);
        Assert.Equal(Path.Combine(_directory, "xray"), standalone.BinaryPath);
        var config = await File.ReadAllTextAsync(standalone.ConfigPath, Token);
        Assert.Contains($"\"port\": {_port + 1}", config, StringComparison.Ordinal);
        Assert.Contains("out-update-proxy", config, StringComparison.Ordinal);
        var test = Assert.Single(_runner.Calls, call => call.Arguments is ["run", "-test", "-c", _]);
        Assert.EndsWith(".json", test.Arguments[3], StringComparison.Ordinal);
    }

    /// <summary>
    ///     Xray отклонил конфигурацию самообновления — null, скрипт пойдёт напрямую
    /// </summary>
    [Fact]
    public async Task PrepareStandaloneAsync_ConfigRejected_ReturnsNull()
    {
        await EnableAsync();
        _runner.Handler = (_, _) => new ProcessResult(1, string.Empty, "Failed to build outbound > invalid password");

        var standalone = await _sut.PrepareStandaloneAsync(Token);

        Assert.Null(standalone);
        Assert.False(File.Exists(Path.Combine(_directory, "work", UpdateProxyService.StandaloneConfigFileName)));
    }

    /// <summary>
    ///     Состояние считается заново: после восстановления Xray прежняя причина не показывается
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_AfterXrayRestarted_NoStaleReason()
    {
        await EnableAsync();
        SetXrayState(XrayProcessState.Stopped);
        await _sut.ResolveAsync(UpdateVia.Auto, Token);
        SetXrayState(XrayProcessState.Running);

        var status = await _sut.GetStatusAsync(Token);

        Assert.Equal(UpdateProxyState.Ready, status.State);
        Assert.Null(status.Reason);
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

    private void SetXrayState(XrayProcessState state) =>
        _xray.GetStatus().Returns(new XrayProcessStatus(state, null, null, 0, null, null));

    private async Task EnableAsync(string serverId = "srv-de", bool enabled = true) =>
        await _settings.UpdateAsync(
            settings => settings with { UpdateProxy = new UpdateProxySettings { Enabled = enabled, ServerId = serverId } },
            Token);
}
