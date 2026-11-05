namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Список локального источника с текстом для редактора
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Text">Текст в формате v2fly</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
public sealed record LocalListDto(string Name, string Text, DateTimeOffset UpdatedAt);
