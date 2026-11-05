using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Contracts.Connections;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="KeeneticInterfaceCommands"/>: команды совпадают с проверенными на роутере (keenetic-findings §40–60)
/// </summary>
public sealed class KeeneticInterfaceCommandsTests
{
    /// <summary>
    ///     Socks-интерфейс создаётся и включается
    /// </summary>
    [Fact]
    public void Configure_SocksWithBringUp_ReturnsProxyCommands()
    {
        var commands = KeeneticInterfaceCommands.Configure(ConnectionTestData.Socks, ConnectionTestData.Parameters, bringUp: true);

        Assert.Equal(
            [
                "interface Proxy9",
                "interface Proxy9 description \"FlowGate: YouTube\"",
                "interface Proxy9 security-level public",
                "interface Proxy9 proxy protocol socks5",
                "interface Proxy9 proxy upstream 127.0.0.1 10899",
                "interface Proxy9 proxy connect",
                "interface Proxy9 up",
            ],
            commands);
    }

    /// <summary>
    ///     HTTP-интерфейс отличается только протоколом
    /// </summary>
    [Fact]
    public void Configure_Http_UsesHttpProtocol()
    {
        var http = ConnectionTestData.Socks with { Kind = ConnectionKind.Http };

        var commands = KeeneticInterfaceCommands.Configure(http, ConnectionTestData.Parameters, bringUp: true);

        Assert.Contains("interface Proxy9 proxy protocol http", commands);
    }

    /// <summary>
    ///     WireGuard-интерфейс получает адрес, ключ клиента и peer на inbound Xray; без включения — без <c>up</c>
    /// </summary>
    [Fact]
    public void Configure_WireguardWithoutBringUp_ReturnsPeerCommandsWithoutUp()
    {
        var peer = $"interface Wireguard9 wireguard peer {ConnectionTestData.FixtureServerPublicKey}";

        var commands = KeeneticInterfaceCommands.Configure(ConnectionTestData.Wireguard, ConnectionTestData.Parameters, bringUp: false);

        Assert.Equal(
            [
                "interface Wireguard9",
                "interface Wireguard9 description \"FlowGate: Игры\"",
                "interface Wireguard9 security-level public",
                "interface Wireguard9 ip address 10.255.0.99 255.255.255.255",
                "interface Wireguard9 ip mtu 1420",
                "interface Wireguard9 wireguard private-key client-private",
                peer,
                $"{peer} endpoint 127.0.0.1:10898",
                $"{peer} allow-ips 0.0.0.0 0.0.0.0",
                $"{peer} keepalive-interval 25",
            ],
            commands);
    }

    /// <summary>
    ///     Интерфейс из фикстуры роутера совпадает с соединением — расхождений нет
    /// </summary>
    [Fact]
    public void FindDrift_WireguardFixtureMatches_ReturnsEmpty()
    {
        var connection = ConnectionTestData.Wireguard with { Name = "fixture-wg" };
        var state = KeeneticInterfaceParser.Parse("Wireguard9", RciFixtures.ToElement(RciFixtures.ShowInterfaceWireguard));

        Assert.Empty(KeeneticInterfaceCommands.FindDrift(connection, state, ConnectionTestData.Parameters));
    }

    /// <summary>
    ///     Другой порт пира и другое имя — два расхождения
    /// </summary>
    [Fact]
    public void FindDrift_WireguardPortAndNameDiffer_ReturnsBoth()
    {
        var connection = ConnectionTestData.Wireguard with { Port = 10850 };
        var state = KeeneticInterfaceParser.Parse("Wireguard9", RciFixtures.ToElement(RciFixtures.ShowInterfaceWireguard));

        var drift = KeeneticInterfaceCommands.FindDrift(connection, state, ConnectionTestData.Parameters);

        Assert.Equal(["описание", "порт пира WireGuard"], drift);
    }

    /// <summary>
    ///     Адрес upstream сверяется только у подключённого прокси: во время переподключения поле бывает другим
    /// </summary>
    [Fact]
    public void FindDrift_ProxyUpstream_CheckedOnlyWhenConnected()
    {
        var connection = ConnectionTestData.Socks with { Name = "fixture" };
        var state = KeeneticInterfaceParser.Parse("Proxy9", RciFixtures.ToElement(RciFixtures.ShowInterfaceProxy)) with { RemoteEndpointAddress = "10.0.0.1" };

        Assert.Empty(KeeneticInterfaceCommands.FindDrift(connection, state, ConnectionTestData.Parameters));
        Assert.Equal(["адрес upstream"], KeeneticInterfaceCommands.FindDrift(connection, state with { Connected = true }, ConnectionTestData.Parameters));
    }

    /// <summary>
    ///     Прокси-интерфейс из фикстуры совпадает с соединением, порт не сверяется (RCI его не показывает)
    /// </summary>
    [Fact]
    public void FindDrift_ProxyFixtureMatches_ReturnsEmpty()
    {
        var connection = ConnectionTestData.Socks with { Name = "fixture", Port = 10000 };
        var state = KeeneticInterfaceParser.Parse("Proxy9", RciFixtures.ToElement(RciFixtures.ShowInterfaceProxy));

        Assert.Empty(KeeneticInterfaceCommands.FindDrift(connection, state, ConnectionTestData.Parameters));
    }
}
