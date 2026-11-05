namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Список локального источника: текст в формате v2fly, который правится в FlowGate
/// </summary>
/// <param name="SourceId">Локальный источник</param>
/// <param name="Name">Имя списка</param>
/// <param name="Text">Текст списка</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
public sealed record LocalList(string SourceId, string Name, string Text, DateTimeOffset UpdatedAt);
