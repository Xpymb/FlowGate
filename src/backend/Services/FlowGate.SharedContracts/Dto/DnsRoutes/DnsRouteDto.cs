namespace FlowGate.SharedContracts.Dto.DnsRoutes;

/// <summary>
///     DNS-маршрут: список источника → соединение
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="SourceId">Источник</param>
/// <param name="SourceName">Имя источника или null, если источник удалён</param>
/// <param name="ListName">Список</param>
/// <param name="AttributeFilter">Фильтр <c>attr</c> / <c>!attr</c> или null</param>
/// <param name="ConnectionId">Соединение</param>
/// <param name="ConnectionName">Имя соединения или null, если удалено</param>
/// <param name="InterfaceName">Интерфейс Keenetic соединения</param>
/// <param name="GroupName">Основная группа в Keenetic</param>
/// <param name="State"><c>Pending</c> — ещё не создан в Keenetic, <c>Enabled</c>, <c>Disabled</c></param>
/// <param name="Exclusive">Эксклюзивный маршрут: трафик к доменам списка идёт только через соединение, при его недоступности блокируется</param>
/// <param name="LastApply">Итог последнего применения или null</param>
/// <param name="CreatedAt">Когда создан</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
public sealed record DnsRouteDto(
    string Id,
    string SourceId,
    string? SourceName,
    string ListName,
    string? AttributeFilter,
    string ConnectionId,
    string? ConnectionName,
    string? InterfaceName,
    string GroupName,
    string State,
    bool Exclusive,
    DnsRouteApplyDto? LastApply,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
