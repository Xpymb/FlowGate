using FlowGate.Bll.Implementations.Connections;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Dal.Implementations.Connections;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="ConnectionXrayConfigSource"/>: соединения и inbound прокси обновлений в одной модели
/// </summary>
public sealed class ConnectionXrayConfigSourceTests : IDisposable
{
    private const int ProxyPort = 10790;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly ConnectionProvider _connections;
    private readonly SettingsProvider _settings;
    private readonly ConnectionXrayConfigSource _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionXrayConfigSourceTests"/>
    /// </summary>
    public ConnectionXrayConfigSourceTests()
    {
        var storeFactory = new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = _directory }));
        _connections = new ConnectionProvider(storeFactory);
        _settings = new SettingsProvider(storeFactory);
        var servers = new ServerProvider(storeFactory);
        servers.AddAsync([ConnectionTestData.Server()], CancellationToken.None).GetAwaiter().GetResult();

        _sut = new ConnectionXrayConfigSource(
            _connections,
            servers,
            _settings,
            MsOptions.Create(new ConnectionOptions()),
            MsOptions.Create(new UpdateProxyOptions { Port = ProxyPort }),
            MsOptions.Create(new XrayOptions()));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Ни соединений, ни сервера прокси — пустая модель
    /// </summary>
    [Fact]
    public async Task GetModelAsync_Nothing_Empty()
    {
        var model = await _sut.GetModelAsync("warning", Token);

        Assert.Empty(model.Inbounds);
        Assert.Empty(model.Outbounds);
        Assert.Empty(model.Rules);
    }

    /// <summary>
    ///     Соединений нет, сервер прокси выбран (прокси выключен) — в модели только inbound прокси
    /// </summary>
    [Fact]
    public async Task GetModelAsync_OnlyProxyServer_ContainsProxyInbound()
    {
        await SelectProxyServerAsync("srv-de", enabled: false);

        var model = await _sut.GetModelAsync("warning", Token);

        var inbound = Assert.Single(model.Inbounds);
        Assert.Equal(UpdateProxyXrayModel.InboundTag, inbound.Tag);
        Assert.Equal(UpdateProxyXrayModel.ListenAddress, inbound.Listen);
        Assert.Equal(ProxyPort, inbound.Port);
        Assert.Equal(UpdateProxyXrayModel.OutboundTag, Assert.Single(model.Outbounds).Tag);
    }

    /// <summary>
    ///     Соединение и прокси вместе: оба набора inbound/outbound/правил в одной модели
    /// </summary>
    [Fact]
    public async Task GetModelAsync_ConnectionAndProxy_ContainsBoth()
    {
        await _connections.AddAsync(ConnectionTestData.Socks, Token);
        await SelectProxyServerAsync("srv-de");

        var model = await _sut.GetModelAsync("warning", Token);

        Assert.Equal([ConnectionTestData.Socks.InboundTag, UpdateProxyXrayModel.InboundTag], model.Inbounds.Select(inbound => inbound.Tag));
        Assert.Equal(2, model.Rules.Count);
    }

    /// <summary>
    ///     Сервер прокси пропал (ушёл из подписки) — inbound прокси в модели нет
    /// </summary>
    [Fact]
    public async Task GetModelAsync_ProxyServerMissing_NoProxyInbound()
    {
        await _connections.AddAsync(ConnectionTestData.Socks, Token);
        await SelectProxyServerAsync("srv-gone");

        var model = await _sut.GetModelAsync("warning", Token);

        Assert.DoesNotContain(model.Inbounds, inbound => inbound.Tag == UpdateProxyXrayModel.InboundTag);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private async Task SelectProxyServerAsync(string serverId, bool enabled = true) =>
        await _settings.UpdateAsync(
            settings => settings with { UpdateProxy = new UpdateProxySettings { Enabled = enabled, ServerId = serverId } },
            Token);
}
