namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Все маршруты (содержимое <c>dns-routes.json</c>)
/// </summary>
/// <param name="Routes">Маршруты</param>
public sealed record DnsRouteCatalog(IReadOnlyList<DnsRoute> Routes)
{
    /// <summary>
    ///     Пустой каталог
    /// </summary>
    public static DnsRouteCatalog Empty { get; } = new([]);
}
