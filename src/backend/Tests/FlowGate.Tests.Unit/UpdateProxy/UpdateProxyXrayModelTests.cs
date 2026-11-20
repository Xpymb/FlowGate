using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.Tests.Unit.Connections;
using FlowGate.Tests.Unit.Servers;

namespace FlowGate.Tests.Unit.UpdateProxy;

/// <summary>
///     Тесты <see cref="UpdateProxyXrayModel"/>: HTTP-inbound на 127.0.0.1, весь его трафик — через сервер
/// </summary>
public sealed class UpdateProxyXrayModelTests
{
    private readonly XrayConfigBuilder _builder = new();

    /// <summary>
    ///     Конфигурация: один http-inbound, outbound сервера и одно правило без условия по домену
    /// </summary>
    [Fact]
    public void Build_RoutesAllInboundTrafficToServer()
    {
        var model = UpdateProxyXrayModel.Build("warning", ConnectionTestData.Server().Outbound, 10790);

        var config = _builder.Build(model);

        JsonAssert.Equal(
            """
            [ { "tag": "in-update-proxy", "listen": "127.0.0.1", "port": 10790, "protocol": "http", "settings": {} } ]
            """,
            config["inbounds"]);
        JsonAssert.Equal(
            """
            [ { "type": "field", "inboundTag": ["in-update-proxy"], "outboundTag": "out-update-proxy" } ]
            """,
            config["routing"]!["rules"]);
        Assert.Equal("out-update-proxy", config["outbounds"]![2]!["tag"]!.GetValue<string>());
        Assert.Equal("trojan", config["outbounds"]![2]!["protocol"]!.GetValue<string>());
    }

    /// <summary>
    ///     Добавление к модели соединений: прежние элементы сохраняются, прокси — в конце списков
    /// </summary>
    [Fact]
    public void Append_KeepsConnectionsAndAddsProxyLast()
    {
        var connections = new XrayConfigModel(
            "info",
            [new XrayInbound("in-c1", "socks", "0.0.0.0", 10800, null, Sniffing: true)],
            [new XrayOutbound("out-c1", ConnectionTestData.Server().Outbound)],
            [new XrayRoutingRule(["in-c1"], "out-c1")]);

        var model = UpdateProxyXrayModel.Append(connections, ConnectionTestData.Server().Outbound, 10790);

        Assert.Equal("info", model.LogLevel);
        Assert.Equal(["in-c1", UpdateProxyXrayModel.InboundTag], model.Inbounds.Select(inbound => inbound.Tag));
        Assert.Equal(["out-c1", UpdateProxyXrayModel.OutboundTag], model.Outbounds.Select(outbound => outbound.Tag));
        Assert.Equal(UpdateProxyXrayModel.OutboundTag, model.Rules[^1].OutboundTag);
        Assert.Single(connections.Inbounds);
    }

    /// <summary>
    ///     Изменение outbound в модели не меняет сервер
    /// </summary>
    [Fact]
    public void Build_OutboundIsCopy()
    {
        var server = ConnectionTestData.Server();

        var model = UpdateProxyXrayModel.Build("warning", server.Outbound, 10790);
        model.Outbounds[0].Definition["protocol"] = "changed";

        Assert.Equal("trojan", server.Outbound["protocol"]!.GetValue<string>());
        Assert.Single(model.Rules);
    }
}
