namespace FlowGate.SharedContracts.Dto.DnsRoutes;

/// <summary>
///     Создание маршрута
/// </summary>
/// <param name="SourceId">Источник</param>
/// <param name="ListName">Список источника</param>
/// <param name="ConnectionId">Соединение, через которое пойдёт трафик списка</param>
/// <param name="AttributeFilter">Фильтр <c>attr</c> / <c>!attr</c> или null</param>
/// <param name="Exclusive">Эксклюзивный маршрут: трафик списка идёт только через соединение, при его недоступности блокируется</param>
public sealed record DnsRouteCreateRequest(string SourceId, string ListName, string ConnectionId, string? AttributeFilter = null, bool Exclusive = false);
