using System.Text.Json.Nodes;

using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Модель временного Xray для пинга: на каждый сервер socks-inbound <c>ping-in-N</c> на <c>127.0.0.1</c> и правило
///     в его outbound <c>ping-out-N</c>
/// </summary>
public static class PingXrayModel
{
    /// <summary>
    ///     Адрес socks-inbound
    /// </summary>
    public const string ListenAddress = "127.0.0.1";

    /// <summary>
    ///     Собирает модель
    /// </summary>
    /// <param name="targets">Outbound серверов и порты их socks-inbound</param>
    /// <returns>Модель для <c>IXrayConfigBuilder</c></returns>
    public static XrayConfigModel Build(IReadOnlyList<(JsonObject Outbound, int Port)> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);

        var inbounds = targets
            .Select((target, index) => new XrayInbound(
                $"ping-in-{index}",
                "socks",
                ListenAddress,
                target.Port,
                new JsonObject { ["auth"] = "noauth", ["udp"] = false },
                Sniffing: false))
            .ToList();
        var outbounds = targets
            .Select((target, index) => new XrayOutbound($"ping-out-{index}", (JsonObject)target.Outbound.DeepClone()))
            .ToList();
        var rules = targets
            .Select((_, index) => new XrayRoutingRule([$"ping-in-{index}"], $"ping-out-{index}"))
            .ToList();

        return new XrayConfigModel("error", inbounds, outbounds, rules);
    }
}
