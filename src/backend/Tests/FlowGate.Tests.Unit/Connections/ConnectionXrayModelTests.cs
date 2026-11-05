using FlowGate.Bll.Implementations.Connections;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Tests.Unit.Servers;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="ConnectionXrayModel"/>: inbound на соединение, outbound — копия сервера, без сервера — block
/// </summary>
public sealed class ConnectionXrayModelTests
{
    private readonly XrayConfigBuilder _builder = new();

    /// <summary>
    ///     Socks и WireGuard на один сервер дают по inbound, outbound и правилу; конфигурация собирается
    /// </summary>
    [Fact]
    public void Build_SocksAndWireguard_ProducesInboundsOutboundsAndRules()
    {
        var model = ConnectionXrayModel.Build(
            "warning",
            [ConnectionTestData.Socks, ConnectionTestData.Wireguard],
            [ConnectionTestData.Server()],
            "127.0.0.1",
            1420);

        var config = _builder.Build(model);

        JsonAssert.Equal(
            """
            [
              {
                "tag": "in-con-socks", "listen": "127.0.0.1", "port": 10899, "protocol": "socks",
                "settings": { "auth": "noauth", "udp": true },
                "sniffing": { "enabled": true, "destOverride": ["http", "tls", "quic"] }
              },
              {
                "tag": "in-con-wg", "listen": "127.0.0.1", "port": 10898, "protocol": "wireguard",
                "settings": {
                  "secretKey": "server-private", "mtu": 1420,
                  "peers": [ { "publicKey": "client-public", "allowedIPs": ["10.255.0.99/32"] } ]
                },
                "sniffing": { "enabled": true, "destOverride": ["http", "tls", "quic"] }
              }
            ]
            """,
            config["inbounds"]);
        Assert.Equal(["out-con-socks", "out-con-wg"], model.Outbounds.Select(outbound => outbound.Tag));
        Assert.Equal("trojan", config["outbounds"]![2]!["protocol"]!.GetValue<string>());
        Assert.Equal(["out-con-socks", "out-con-wg"], model.Rules.Select(rule => rule.OutboundTag));
    }

    /// <summary>
    ///     Сервер удалён — трафик соединения идёт в block, а не напрямую
    /// </summary>
    [Fact]
    public void Build_ServerMissing_RoutesToBlock()
    {
        var model = ConnectionXrayModel.Build("warning", [ConnectionTestData.Socks], [], "127.0.0.1", 1420);

        Assert.Empty(model.Outbounds);
        Assert.Equal(XrayConfigModel.BlockTag, Assert.Single(model.Rules).OutboundTag);
        _builder.Build(model);
    }

    /// <summary>
    ///     Изменение outbound в модели не меняет исходный сервер
    /// </summary>
    [Fact]
    public void Build_OutboundIsCopy()
    {
        var server = ConnectionTestData.Server();

        var model = ConnectionXrayModel.Build("warning", [ConnectionTestData.Socks], [server], "127.0.0.1", 1420);
        model.Outbounds[0].Definition["protocol"] = "changed";

        Assert.Equal("trojan", server.Outbound["protocol"]!.GetValue<string>());
    }
}
