namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Результат разбора текста списка
/// </summary>
/// <param name="Rules">Разобранные правила</param>
/// <param name="Errors">Ошибки по строкам; строки с ошибками пропущены</param>
public sealed record V2flyParseResult(IReadOnlyList<V2flyRule> Rules, IReadOnlyList<V2flyParseError> Errors);
