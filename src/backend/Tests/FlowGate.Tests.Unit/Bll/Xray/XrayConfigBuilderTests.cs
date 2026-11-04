using System.Text.Json.Nodes;

using FlowGate.Bll.Implementations.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты <see cref="XrayConfigBuilder"/>: сравнение со снимком JSON и проверка модели
/// </summary>
public class XrayConfigBuilderTests
{
    private readonly XrayConfigBuilder _sut = new();

    /// <summary>
    ///     Пустая модель даёт только встроенные outbound
    /// </summary>
    [Fact]
    public void Build_EmptyModel_ReturnsBaseConfig()
    {
        var config = _sut.Build(XrayConfigModel.Empty("warning"));

        AssertJson(
            """
            {
              "log": { "loglevel": "warning" },
              "inbounds": [],
              "outbounds": [
                { "tag": "direct", "protocol": "freedom" },
                { "tag": "block", "protocol": "blackhole" }
              ],
              "routing": { "domainStrategy": "AsIs", "rules": [] }
            }
            """,
            config);
    }

    /// <summary>
    ///     Socks, http и wireguard inbound с outbound и правилами собираются как ожидается
    /// </summary>
    [Fact]
    public void Build_ConnectionsModel_ReturnsFullConfig()
    {
        var model = new XrayConfigModel(
            "info",
            [
                new XrayInbound("in-socks", "socks", "127.0.0.1", 10801, new JsonObject { ["udp"] = true }, Sniffing: true),
                new XrayInbound("in-http", "http", "127.0.0.1", 10802, null, Sniffing: false),
                new XrayInbound("in-wg", "wireguard", "127.0.0.1", 10803, new JsonObject { ["secretKey"] = "KEY" }, Sniffing: true),
            ],
            [new XrayOutbound("out-server", new JsonObject { ["tag"] = "ignored", ["protocol"] = "vless" })],
            [
                new XrayRoutingRule(["in-socks", "in-wg"], "out-server"),
                new XrayRoutingRule(["in-http"], XrayConfigModel.DirectTag),
            ]);

        var config = _sut.Build(model);

        AssertJson(
            """
            {
              "log": { "loglevel": "info" },
              "inbounds": [
                { "tag": "in-socks", "listen": "127.0.0.1", "port": 10801, "protocol": "socks", "settings": { "udp": true },
                  "sniffing": { "enabled": true, "destOverride": ["http", "tls", "quic"] } },
                { "tag": "in-http", "listen": "127.0.0.1", "port": 10802, "protocol": "http", "settings": {} },
                { "tag": "in-wg", "listen": "127.0.0.1", "port": 10803, "protocol": "wireguard", "settings": { "secretKey": "KEY" },
                  "sniffing": { "enabled": true, "destOverride": ["http", "tls", "quic"] } }
              ],
              "outbounds": [
                { "tag": "direct", "protocol": "freedom" },
                { "tag": "block", "protocol": "blackhole" },
                { "tag": "out-server", "protocol": "vless" }
              ],
              "routing": {
                "domainStrategy": "AsIs",
                "rules": [
                  { "type": "field", "inboundTag": ["in-socks", "in-wg"], "outboundTag": "out-server" },
                  { "type": "field", "inboundTag": ["in-http"], "outboundTag": "direct" }
                ]
              }
            }
            """,
            config);
    }

    /// <summary>
    ///     Сборка не меняет объекты модели
    /// </summary>
    [Fact]
    public void Build_OutboundDefinition_IsNotMutated()
    {
        var definition = new JsonObject { ["protocol"] = "vless" };
        var model = new XrayConfigModel("warning", [], [new XrayOutbound("out", definition)], []);

        _sut.Build(model);

        Assert.False(definition.ContainsKey("tag"));
    }

    /// <summary>
    ///     Противоречивые модели отклоняются
    /// </summary>
    [Theory]
    [MemberData(nameof(InvalidModels))]
    public void Build_InvalidModel_ThrowsXrayOperationException(XrayConfigModel model, string expectedReason)
    {
        var exception = Assert.Throws<XrayOperationException>(() => _sut.Build(model));

        Assert.Contains(expectedReason, exception.Message);
    }

    /// <summary>
    ///     Набор противоречивых моделей и ожидаемых причин
    /// </summary>
    /// <returns>Модель и фрагмент сообщения</returns>
    public static TheoryData<XrayConfigModel, string> InvalidModels()
    {
        var socks = new XrayInbound("in", "socks", "127.0.0.1", 1080, null, false);
        return new TheoryData<XrayConfigModel, string>
        {
            { XrayConfigModel.Empty("verbose"), "уровень логирования" },
            { new XrayConfigModel("info", [socks, socks], [], [new XrayRoutingRule(["in"], "direct")]), "повторяется" },
            { new XrayConfigModel("info", [], [new XrayOutbound("direct", new JsonObject())], []), "зарезервирован" },
            { new XrayConfigModel("info", [socks], [], []), "нет правила" },
            { new XrayConfigModel("info", [socks], [], [new XrayRoutingRule(["in"], "missing")]), "несуществующий outbound" },
            { new XrayConfigModel("info", [], [], [new XrayRoutingRule(["ghost"], "direct")]), "несуществующий inbound" },
        };
    }

    private static void AssertJson(string expected, JsonNode actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actual),
            $"Ожидалось:{Environment.NewLine}{expectedNode}{Environment.NewLine}Получено:{Environment.NewLine}{actual}");
    }
}
