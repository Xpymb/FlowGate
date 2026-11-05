namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Вид элемента списка. Keenetic держит все виды в одной группе <c>object-group fqdn</c>
/// </summary>
public enum ListEntryKind
{
    /// <summary>
    ///     Домен вместе с поддоменами
    /// </summary>
    Domain,

    /// <summary>
    ///     IPv4-адрес или подсеть
    /// </summary>
    Ipv4,

    /// <summary>
    ///     IPv6-адрес или подсеть
    /// </summary>
    Ipv6,
}
