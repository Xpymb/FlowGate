using FlowGate.Contracts.DomainLists;

namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Желаемое состояние маршрута для планировщика
/// </summary>
/// <param name="Route">Маршрут</param>
/// <param name="InterfaceName">Интерфейс соединения маршрута</param>
/// <param name="Description">Описание основной группы, например <c>FlowGate: youtube (v2fly)</c></param>
/// <param name="Content">Содержимое списка или null, если список сейчас недоступен (тогда состав групп не меняется)</param>
public sealed record DnsRouteTarget(DnsRoute Route, string InterfaceName, string Description, ListContent? Content);
