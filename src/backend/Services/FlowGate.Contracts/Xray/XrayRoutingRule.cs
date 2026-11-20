namespace FlowGate.Contracts.Xray;

/// <summary>
///     Правило маршрутизации Xray: трафик из этих inbound (и, если заданы, к этим доменам) идёт в этот outbound или
///     балансировщик
/// </summary>
/// <param name="InboundTags">Теги inbound</param>
/// <param name="OutboundTag">Тег outbound; null, если задан <paramref name="BalancerTag"/></param>
/// <param name="Domains">Домены в формате Xray (<c>domain:</c>, <c>full:</c>); null или пусто — без условия по домену</param>
/// <param name="BalancerTag">Тег балансировщика вместо outbound</param>
public sealed record XrayRoutingRule(
    IReadOnlyList<string> InboundTags,
    string? OutboundTag,
    IReadOnlyList<string>? Domains = null,
    string? BalancerTag = null)
{
    /// <summary>
    ///     Создаёт правило, которое ведёт трафик inbound в балансировщик
    /// </summary>
    /// <param name="inboundTags">Теги inbound</param>
    /// <param name="balancerTag">Тег балансировщика</param>
    /// <returns>Правило</returns>
    public static XrayRoutingRule ToBalancer(IReadOnlyList<string> inboundTags, string balancerTag) =>
        new(inboundTags, OutboundTag: null, BalancerTag: balancerTag);
}
