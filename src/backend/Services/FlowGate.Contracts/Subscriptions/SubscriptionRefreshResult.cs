namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Результат последнего обновления подписки
/// </summary>
/// <param name="At">Когда обновлялась</param>
/// <param name="Status">Итог</param>
/// <param name="Error">Ошибка; без адреса подписки и учётных данных</param>
/// <param name="Received">Узлов в ответе</param>
/// <param name="Accepted">Разобрано</param>
/// <param name="Rejected">Отклонено</param>
/// <param name="Added">Добавлено серверов</param>
/// <param name="Updated">Изменено серверов</param>
/// <param name="Removed">Удалено серверов</param>
/// <param name="Retained">Сохранено серверов, пропавших из частичного ответа</param>
/// <param name="Warnings">Предупреждения разбора</param>
public sealed record SubscriptionRefreshResult(
    DateTimeOffset At,
    SubscriptionRefreshStatus Status,
    string? Error,
    int Received,
    int Accepted,
    int Rejected,
    int Added,
    int Updated,
    int Removed,
    int Retained,
    IReadOnlyList<string> Warnings)
{
    /// <summary>
    ///     Неудачное обновление без счётчиков
    /// </summary>
    /// <param name="at">Когда обновлялась</param>
    /// <param name="error">Ошибка</param>
    /// <returns>Результат со статусом <see cref="SubscriptionRefreshStatus.Failed"/></returns>
    public static SubscriptionRefreshResult Failure(DateTimeOffset at, string error) =>
        new(at, SubscriptionRefreshStatus.Failed, error, 0, 0, 0, 0, 0, 0, 0, []);
}
