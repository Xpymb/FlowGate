namespace FlowGate.Contracts.Xray;

/// <summary>
///     Правило маршрутизации Xray: трафик из этих inbound (и, если заданы, к этим доменам) идёт в этот outbound
/// </summary>
/// <param name="InboundTags">Теги inbound</param>
/// <param name="OutboundTag">Тег outbound</param>
/// <param name="Domains">Домены в формате Xray (<c>domain:</c>, <c>full:</c>); null или пусто — без условия по домену</param>
public sealed record XrayRoutingRule(IReadOnlyList<string> InboundTags, string OutboundTag, IReadOnlyList<string>? Domains = null);
