namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Результат планирования: команды NDMS и итог по каждому маршруту
/// </summary>
/// <param name="Commands">Команды по порядку; пусто — всё уже совпадает</param>
/// <param name="Outcomes">Итоги по маршрутам</param>
public sealed record DnsRoutePlan(IReadOnlyList<string> Commands, IReadOnlyList<DnsRouteOutcome> Outcomes);
