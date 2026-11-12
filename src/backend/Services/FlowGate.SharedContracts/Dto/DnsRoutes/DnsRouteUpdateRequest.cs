namespace FlowGate.SharedContracts.Dto.DnsRoutes;

/// <summary>
///     Изменение маршрута: соединение, фильтр и эксклюзивность. Список и источник не меняются — для этого создайте новый маршрут
/// </summary>
/// <param name="ConnectionId">Соединение</param>
/// <param name="AttributeFilter">Фильтр или null</param>
/// <param name="Exclusive">Эксклюзивный маршрут; null — не менять</param>
public sealed record DnsRouteUpdateRequest(string ConnectionId, string? AttributeFilter = null, bool? Exclusive = null);
