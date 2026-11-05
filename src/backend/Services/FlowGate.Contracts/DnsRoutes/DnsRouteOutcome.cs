namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Итог планирования одного маршрута
/// </summary>
/// <param name="RouteId">Маршрут</param>
/// <param name="Kind">Что произошло</param>
/// <param name="Disabled">Правило основной группы после применения выключено</param>
/// <param name="Parts">Частей группы после применения</param>
/// <param name="Entries">Элементов в группах после применения</param>
public sealed record DnsRouteOutcome(string RouteId, DnsRouteOutcomeKind Kind, bool Disabled, int Parts, int Entries);
