namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Итог последнего применения маршрута в Keenetic
/// </summary>
/// <param name="At">Когда</param>
/// <param name="Status">Результат</param>
/// <param name="Error">Ошибка или null</param>
/// <param name="Entries">Элементов в группах</param>
/// <param name="Skipped">Пропущено правил списка</param>
/// <param name="Parts">Частей группы</param>
/// <param name="Warnings">Предупреждения, например пересечение с маршрутом на другое соединение</param>
/// <param name="ContentChangedAt">
///     Когда состав групп в Keenetic последний раз изменился; null — изменений не было с момента, когда FlowGate начал
///     это отслеживать
/// </param>
public sealed record DnsRouteApply(
    DateTimeOffset At,
    DnsRouteApplyStatus Status,
    string? Error,
    int Entries,
    int Skipped,
    int Parts,
    IReadOnlyList<string> Warnings,
    DateTimeOffset? ContentChangedAt = null);
