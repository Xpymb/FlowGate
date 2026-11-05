namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Итог обновления источника
/// </summary>
/// <param name="At">Когда</param>
/// <param name="Status"><c>Ok</c>, <c>Partial</c> или <c>Failed</c></param>
/// <param name="Error">Ошибка или null</param>
/// <param name="Lists">Списков в каталоге</param>
/// <param name="Errors">Ошибки отдельных списков, например <c>data/foo: строка 12: …</c></param>
public sealed record DomainSourceRefreshDto(DateTimeOffset At, string Status, string? Error, int Lists, IReadOnlyList<string> Errors);
