namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Все списки локальных источников (содержимое <c>local-lists.json</c>)
/// </summary>
/// <param name="Lists">Списки</param>
public sealed record LocalListCatalog(IReadOnlyList<LocalList> Lists)
{
    /// <summary>
    ///     Пустой каталог
    /// </summary>
    public static LocalListCatalog Empty { get; } = new([]);
}
