namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Все источники (содержимое <c>domain-sources.json</c>)
/// </summary>
/// <param name="Sources">Источники</param>
public sealed record DomainSourceCatalog(IReadOnlyList<DomainSource> Sources)
{
    /// <summary>
    ///     Каталог первого запуска: только встроенные источники
    /// </summary>
    public static DomainSourceCatalog Default => new(DomainSource.BuiltInSources);
}
