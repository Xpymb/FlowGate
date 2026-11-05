using System.Text.Json.Nodes;

using FlowGate.Contracts.Xray;

namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Модель конфигурации прокси обновлений: HTTP-inbound на <c>127.0.0.1</c>, outbound выбранного сервера,
///     домены обновлений — через сервер, всё остальное — напрямую
/// </summary>
public static class UpdateProxyXrayModel
{
    /// <summary>
    ///     Тег inbound прокси обновлений
    /// </summary>
    public const string InboundTag = "in-update-proxy";

    /// <summary>
    ///     Тег outbound выбранного сервера
    /// </summary>
    public const string OutboundTag = "out-update-proxy";

    /// <summary>
    ///     Адрес прослушивания: прокси нужен только самому FlowGate
    /// </summary>
    public const string ListenAddress = "127.0.0.1";

    /// <summary>
    ///     Строит модель
    /// </summary>
    /// <param name="logLevel">Уровень логирования Xray</param>
    /// <param name="outbound">Outbound сервера; копируется</param>
    /// <param name="domains">Домены, которые идут через сервер</param>
    /// <param name="port">Порт HTTP-inbound</param>
    /// <returns>Модель для <c>IXrayConfigBuilder</c></returns>
    public static XrayConfigModel Build(string logLevel, JsonObject outbound, IReadOnlyList<string> domains, int port)
    {
        ArgumentNullException.ThrowIfNull(outbound);
        ArgumentNullException.ThrowIfNull(domains);

        var rules = new List<XrayRoutingRule>();
        if (domains.Count > 0)
        {
            rules.Add(new XrayRoutingRule([InboundTag], OutboundTag, domains));
        }

        rules.Add(new XrayRoutingRule([InboundTag], XrayConfigModel.DirectTag));

        return new XrayConfigModel(
            logLevel,
            [new XrayInbound(InboundTag, "http", ListenAddress, port, null, Sniffing: false)],
            [new XrayOutbound(OutboundTag, (JsonObject)outbound.DeepClone())],
            rules);
    }
}
