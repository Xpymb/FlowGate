namespace FlowGate.SharedContracts.Dto.DnsRoutes;

/// <summary>
///     Изменение маршрута: соединение и фильтр. Список и источник не меняются — для этого создайте новый маршрут
/// </summary>
/// <param name="ConnectionId">Соединение</param>
/// <param name="AttributeFilter">Фильтр или null</param>
public sealed record DnsRouteUpdateRequest(string ConnectionId, string? AttributeFilter = null);
