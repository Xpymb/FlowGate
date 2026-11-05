namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Итог последнего обновления источника
/// </summary>
/// <param name="At">Когда</param>
/// <param name="Status">Результат</param>
/// <param name="Error">Ошибка или null</param>
/// <param name="Lists">Списков в каталоге</param>
/// <param name="Errors">Ошибки отдельных списков вида <c>data/foo: строка 12: …</c></param>
public sealed record DomainSourceRefresh(
    DateTimeOffset At,
    DomainSourceRefreshStatus Status,
    string? Error,
    int Lists,
    IReadOnlyList<string> Errors);
