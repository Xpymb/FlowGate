namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Итог планирования одного маршрута
/// </summary>
/// <param name="RouteId">Маршрут</param>
/// <param name="Kind">Что произошло</param>
/// <param name="Disabled">Правило основной группы после применения выключено</param>
/// <param name="Exclusive">Правила маршрута после применения эксклюзивные (<c>reject</c>)</param>
/// <param name="Parts">Частей группы после применения</param>
/// <param name="Entries">Элементов в группах после применения</param>
public sealed record DnsRouteOutcome(string RouteId, DnsRouteOutcomeKind Kind, bool Disabled, bool Exclusive, int Parts, int Entries);
