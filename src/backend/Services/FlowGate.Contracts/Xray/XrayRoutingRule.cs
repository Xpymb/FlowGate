namespace FlowGate.Contracts.Xray;

/// <summary>
///     Правило маршрутизации Xray: трафик из этих inbound идёт в этот outbound
/// </summary>
/// <param name="InboundTags">Теги inbound</param>
/// <param name="OutboundTag">Тег outbound</param>
public sealed record XrayRoutingRule(IReadOnlyList<string> InboundTags, string OutboundTag);
