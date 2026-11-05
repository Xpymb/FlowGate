namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Текст списка локального источника
/// </summary>
/// <param name="Text">Текст в формате v2fly: домен, <c>full:</c>, адрес или подсеть на строку, <c>#</c> — комментарий</param>
public sealed record LocalListSaveRequest(string Text);
