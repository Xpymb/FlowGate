using System.Text.Json.Nodes;

using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Exceptions;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Keenetic;

/// <summary>
///     Тесты <see cref="KeeneticInterfaceParser"/> на ответах <c>show/interface</c>, снятых с роутера
/// </summary>
public sealed class KeeneticInterfaceParserTests
{
    /// <summary>
    ///     Прокси-интерфейс: описание, состояние и адрес upstream
    /// </summary>
    [Fact]
    public void Parse_ProxyFixture_ReadsStateAndEndpoint()
    {
        var state = KeeneticInterfaceParser.Parse("Proxy9", RciFixtures.ToElement(RciFixtures.ShowInterfaceProxy));

        Assert.Equal("Proxy", state.Type);
        Assert.Equal("FlowGate: fixture", state.Description);
        Assert.Equal("up", state.State);
        Assert.Equal("down", state.Link);
        Assert.False(state.Connected);
        Assert.Equal("127.0.0.1", state.RemoteEndpointAddress);
        Assert.Empty(state.WireguardPeers);
    }

    /// <summary>
    ///     WireGuard-интерфейс: адрес и peer; <c>last-handshake</c> = 2147483647 означает «ни разу»
    /// </summary>
    [Fact]
    public void Parse_WireguardFixture_ReadsPeerWithoutHandshake()
    {
        var state = KeeneticInterfaceParser.Parse("Wireguard9", RciFixtures.ToElement(RciFixtures.ShowInterfaceWireguard));

        var peer = Assert.Single(state.WireguardPeers);
        Assert.Equal("10.255.0.99", state.Address);
        Assert.Equal("Kp5mU8LXHf4mVJ2EgkG+K2B9eFdDJzSpOIU6bMgItXo=", peer.PublicKey);
        Assert.Equal("127.0.0.1", peer.RemoteAddress);
        Assert.Equal(10898, peer.RemotePort);
        Assert.False(peer.Online);
        Assert.Null(peer.LastHandshakeSecondsAgo);
    }

    /// <summary>
    ///     Весь ответ разбирается в словарь по имени без учёта регистра
    /// </summary>
    [Fact]
    public void ParseAll_SeveralInterfaces_IndexesByNameIgnoringCase()
    {
        var all = new JsonObject
        {
            ["Proxy9"] = JsonNode.Parse(RciFixtures.ShowInterfaceProxy),
            ["Wireguard9"] = JsonNode.Parse(RciFixtures.ShowInterfaceWireguard),
        };

        var states = KeeneticInterfaceParser.ParseAll(RciFixtures.ToElement(all.ToJsonString()));

        Assert.Equal(2, states.Count);
        Assert.Equal("Wireguard", states["wireguard9"].Type);
    }

    /// <summary>
    ///     Ответ не объект — RCI неисправен
    /// </summary>
    [Fact]
    public void ParseAll_NotObject_Throws()
    {
        Assert.Throws<KeeneticUnavailableException>(() => KeeneticInterfaceParser.ParseAll(RciFixtures.ToElement("[]")));
    }
}
