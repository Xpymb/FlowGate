namespace FlowGate.SharedContracts.Dto.DnsRoutes;

/// <summary>
///     Итог применения маршрута
/// </summary>
/// <param name="At">Когда</param>
/// <param name="Status"><c>Ok</c>, <c>Waiting</c> (источник не загружен) или <c>Failed</c></param>
/// <param name="Error">Ошибка или null</param>
/// <param name="Entries">Элементов в Keenetic</param>
/// <param name="Skipped">Пропущено правил списка</param>
/// <param name="Parts">Частей группы (по 1000 элементов)</param>
/// <param name="Warnings">Предупреждения</param>
/// <param name="ContentChangedAt">Когда состав групп в Keenetic последний раз изменился или null, если неизвестно</param>
public sealed record DnsRouteApplyDto(
    DateTimeOffset At,
    string Status,
    string? Error,
    int Entries,
    int Skipped,
    int Parts,
    IReadOnlyList<string> Warnings,
    DateTimeOffset? ContentChangedAt);
