using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Tests.Unit.Connections;
using FlowGate.Tests.Unit.Servers;

namespace FlowGate.Tests.Unit.UpdateProxy;

/// <summary>
///     Тесты <see cref="UpdateProxyXrayModel"/>: HTTP-inbound на 127.0.0.1, домены — через сервер, остальное — напрямую
/// </summary>
public sealed class UpdateProxyXrayModelTests
{
    private readonly XrayConfigBuilder _builder = new();

    /// <summary>
    ///     Конфигурация: один http-inbound, outbound сервера, правило доменов и правило по умолчанию в direct
    /// </summary>
    [Fact]
    public void Build_WithDomains_RoutesDomainsToServerAndRestDirect()
    {
        var model = UpdateProxyXrayModel.Build("warning", ConnectionTestData.Server().Outbound, ["domain:github.com", "full:sub.example.com"], 10790);

        var config = _builder.Build(model);

        JsonAssert.Equal(
            """
            [ { "tag": "in-update-proxy", "listen": "127.0.0.1", "port": 10790, "protocol": "http", "settings": {} } ]
            """,
            config["inbounds"]);
        JsonAssert.Equal(
            """
            [
              { "type": "field", "inboundTag": ["in-update-proxy"], "domain": ["domain:github.com", "full:sub.example.com"], "outboundTag": "out-update-proxy" },
              { "type": "field", "inboundTag": ["in-update-proxy"], "outboundTag": "direct" }
            ]
            """,
            config["routing"]!["rules"]);
        Assert.Equal("out-update-proxy", config["outbounds"]![2]!["tag"]!.GetValue<string>());
        Assert.Equal("trojan", config["outbounds"]![2]!["protocol"]!.GetValue<string>());
    }

    /// <summary>
    ///     Изменение outbound в модели не меняет сервер
    /// </summary>
    [Fact]
    public void Build_OutboundIsCopy()
    {
        var server = ConnectionTestData.Server();

        var model = UpdateProxyXrayModel.Build("warning", server.Outbound, [], 10790);
        model.Outbounds[0].Definition["protocol"] = "changed";

        Assert.Equal("trojan", server.Outbound["protocol"]!.GetValue<string>());
        Assert.Single(model.Rules);
    }
}
