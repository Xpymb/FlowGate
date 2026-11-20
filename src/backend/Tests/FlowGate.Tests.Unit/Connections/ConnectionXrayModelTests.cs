using FlowGate.Bll.Implementations.Connections;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Options;
using FlowGate.Tests.Unit.Servers;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="ConnectionXrayModel"/>: inbound на соединение, outbound — копия сервера, без сервера — block,
///     с резервными серверами — балансировщик и observatory
/// </summary>
public sealed class ConnectionXrayModelTests
{
    private const int ApiPort = 10792;

    private static readonly ConnectionOptions Options = new();

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
            Options,
            ApiPort);

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
        var model = ConnectionXrayModel.Build("warning", [ConnectionTestData.Socks], [], Options, ApiPort);

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

        var model = ConnectionXrayModel.Build("warning", [ConnectionTestData.Socks], [server], Options, ApiPort);
        model.Outbounds[0].Definition["protocol"] = "changed";

        Assert.Equal("trojan", server.Outbound["protocol"]!.GetValue<string>());
    }

    /// <summary>
    ///     Без резервных серверов балансировщиков и observatory нет
    /// </summary>
    [Fact]
    public void Build_NoFallbacks_HasNoBalancersAndObservatory()
    {
        var model = ConnectionXrayModel.Build("warning", [ConnectionTestData.Socks], [ConnectionTestData.Server()], Options, ApiPort);

        Assert.Empty(model.Balancers);
        Assert.Null(model.Observatory);
        Assert.Null(model.Api);
        Assert.Null(_builder.Build(model)["burstObservatory"]);
        Assert.Null(_builder.Build(model)["api"]);
    }

    /// <summary>
    ///     Основной и два резервных: outbound на каждого, балансировщик с весами по приоритету, fallbackTag — основной,
    ///     правило ведёт в балансировщик, observatory следит за всеми кандидатами
    /// </summary>
    [Fact]
    public void Build_WithFallbacks_ProducesBalancerAndObservatory()
    {
        var connection = ConnectionTestData.Socks with { FallbackServerIds = ["srv-nl", "srv-fi"] };
        var servers = new[] { ConnectionTestData.Server(), ConnectionTestData.Server("srv-nl"), ConnectionTestData.Server("srv-fi") };

        var model = ConnectionXrayModel.Build("warning", [connection], servers, Options, ApiPort);
        var config = _builder.Build(model);

        Assert.Equal(["out-con-socks", "out-con-socks-1", "out-con-socks-2"], model.Outbounds.Select(outbound => outbound.Tag));
        JsonAssert.Equal(
            """
            [
              {
                "tag": "bal-con-socks",
                "selector": ["out-con-socks", "out-con-socks-1", "out-con-socks-2"],
                "fallbackTag": "out-con-socks",
                "strategy": {
                  "type": "leastLoad",
                  "settings": {
                    "expected": 1,
                    "costs": [
                      { "regexp": true, "match": "^out-con-socks$", "value": 1 },
                      { "regexp": true, "match": "^out-con-socks-1$", "value": 100 },
                      { "regexp": true, "match": "^out-con-socks-2$", "value": 10000 }
                    ],
                    "tolerance": 0.5
                  }
                }
              }
            ]
            """,
            config["routing"]!["balancers"]);
        JsonAssert.Equal(
            """
            { "type": "field", "inboundTag": ["in-con-socks"], "balancerTag": "bal-con-socks" }
            """,
            config["routing"]!["rules"]![0]);
        JsonAssert.Equal(
            """
            {
              "subjectSelector": ["out-con-socks", "out-con-socks-1", "out-con-socks-2"],
              "pingConfig": { "destination": "https://www.gstatic.com/generate_204", "interval": "60s", "sampling": 3, "timeout": "30s" }
            }
            """,
            config["burstObservatory"]);
        JsonAssert.Equal(
            """
            { "tag": "api", "listen": "127.0.0.1:10792", "services": ["RoutingService"] }
            """,
            config["api"]);
    }

    /// <summary>
    ///     Основной сервер удалён: кандидаты — оставшиеся резервные, теги сохраняют слоты, fallbackTag — первый живой
    /// </summary>
    [Fact]
    public void Build_PrimaryMissing_KeepsSlotTagsAndUsesFirstAliveAsFallback()
    {
        var connection = ConnectionTestData.Socks with { FallbackServerIds = ["srv-nl", "srv-fi"] };
        var servers = new[] { ConnectionTestData.Server("srv-nl"), ConnectionTestData.Server("srv-fi") };

        var model = ConnectionXrayModel.Build("warning", [connection], servers, Options, ApiPort);
        _builder.Build(model);

        var balancer = Assert.Single(model.Balancers);
        Assert.Equal(["out-con-socks-1", "out-con-socks-2"], balancer.Selector);
        Assert.Equal("out-con-socks-1", balancer.FallbackTag);
        Assert.Equal(1, balancer.Costs["out-con-socks-1"]);
    }

    /// <summary>
    ///     Остался один кандидат — прямое правило в его outbound, без балансировщика
    /// </summary>
    [Fact]
    public void Build_SingleAliveCandidate_RoutesDirectly()
    {
        var connection = ConnectionTestData.Socks with { FallbackServerIds = ["srv-nl"] };

        var model = ConnectionXrayModel.Build("warning", [connection], [ConnectionTestData.Server("srv-nl")], Options, ApiPort);
        _builder.Build(model);

        Assert.Empty(model.Balancers);
        Assert.Null(model.Observatory);
        Assert.Equal("out-con-socks-1", Assert.Single(model.Rules).OutboundTag);
    }

    /// <summary>
    ///     Ни основного, ни резервных серверов нет — block
    /// </summary>
    [Fact]
    public void Build_AllCandidatesMissing_RoutesToBlock()
    {
        var connection = ConnectionTestData.Socks with { FallbackServerIds = ["srv-nl"] };

        var model = ConnectionXrayModel.Build("warning", [connection], [], Options, ApiPort);

        Assert.Empty(model.Outbounds);
        Assert.Equal(XrayConfigModel.BlockTag, Assert.Single(model.Rules).OutboundTag);
    }
}
