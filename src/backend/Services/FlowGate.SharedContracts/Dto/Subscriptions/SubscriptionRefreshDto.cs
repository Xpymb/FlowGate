namespace FlowGate.SharedContracts.Dto.Subscriptions;

/// <summary>
///     Результат последнего обновления подписки
/// </summary>
/// <param name="At">Когда обновлялась</param>
/// <param name="Status"><c>Ok</c>, <c>Partial</c> (часть узлов не разобрана, пропавшие серверы сохранены) или <c>Failed</c></param>
/// <param name="Error">Ошибка</param>
/// <param name="Received">Узлов в ответе</param>
/// <param name="Accepted">Разобрано</param>
/// <param name="Rejected">Отклонено</param>
/// <param name="Added">Добавлено серверов</param>
/// <param name="Updated">Изменено</param>
/// <param name="Removed">Удалено</param>
/// <param name="Retained">Сохранено пропавших из частичного ответа</param>
/// <param name="Warnings">Предупреждения</param>
public sealed record SubscriptionRefreshDto(
    DateTimeOffset At,
    string Status,
    string? Error,
    int Received,
    int Accepted,
    int Rejected,
    int Added,
    int Updated,
    int Removed,
    int Retained,
    IReadOnlyList<string> Warnings);
