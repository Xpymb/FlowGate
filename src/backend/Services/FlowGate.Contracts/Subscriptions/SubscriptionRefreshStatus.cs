namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Итог обновления подписки
/// </summary>
public enum SubscriptionRefreshStatus
{
    /// <summary>
    ///     Все узлы разобраны, каталог приведён к ответу провайдера
    /// </summary>
    Ok,

    /// <summary>
    ///     Часть узлов не разобрана: пропавшие из ответа серверы сохранены
    /// </summary>
    Partial,

    /// <summary>
    ///     Обновление не удалось, прежние серверы не тронуты
    /// </summary>
    Failed,
}
