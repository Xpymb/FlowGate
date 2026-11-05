namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Результат применения маршрута
/// </summary>
public enum DnsRouteApplyStatus
{
    /// <summary>
    ///     Группы и правило в Keenetic соответствуют списку
    /// </summary>
    Ok,

    /// <summary>
    ///     Список пока недоступен (источник не загружен или список не собрался); в Keenetic ничего не менялось
    /// </summary>
    Waiting,

    /// <summary>
    ///     Применить не удалось
    /// </summary>
    Failed,
}
