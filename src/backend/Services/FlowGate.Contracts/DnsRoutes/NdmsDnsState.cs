namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Группы и DNS-маршруты, как их видит Keenetic (<c>GET /rci/object-group/fqdn</c> и <c>/rci/dns-proxy/route</c>)
/// </summary>
/// <param name="Groups">Группы по имени</param>
/// <param name="Routes">Правила в порядке Keenetic</param>
public sealed record NdmsDnsState(IReadOnlyDictionary<string, NdmsGroup> Groups, IReadOnlyList<NdmsRoute> Routes)
{
    /// <summary>
    ///     Пустое состояние
    /// </summary>
    public static NdmsDnsState Empty { get; } = new(new Dictionary<string, NdmsGroup>(StringComparer.Ordinal), []);

    /// <summary>
    ///     Возвращает правило группы на интерфейс
    /// </summary>
    /// <param name="group">Группа</param>
    /// <param name="interfaceName">Интерфейс</param>
    /// <returns>Правило или null</returns>
    public NdmsRoute? FindRoute(string group, string interfaceName) =>
        Routes.FirstOrDefault(route => route.Group == group && string.Equals(route.Interface, interfaceName, StringComparison.OrdinalIgnoreCase));
}
