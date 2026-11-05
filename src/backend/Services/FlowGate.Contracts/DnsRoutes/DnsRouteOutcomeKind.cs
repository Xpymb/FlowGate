namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Что планировщик сделал с маршрутом
/// </summary>
public enum DnsRouteOutcomeKind
{
    /// <summary>
    ///     Группы и правило приведены к списку (или уже совпадали)
    /// </summary>
    Applied,

    /// <summary>
    ///     Список пока недоступен, а групп ещё нет — ждём загрузки источника
    /// </summary>
    Waiting,

    /// <summary>
    ///     Пользователь удалил основную группу в Keenetic: маршрут удаляется и из FlowGate
    /// </summary>
    RemovedByUser,
}
