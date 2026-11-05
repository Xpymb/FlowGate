namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Список в каталоге источника
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Domains">Доменов</param>
/// <param name="Ipv4">IPv4-адресов и подсетей</param>
/// <param name="Ipv6">IPv6-адресов и подсетей</param>
/// <param name="Skipped">Пропущено правил (Keenetic их не поддерживает)</param>
/// <param name="Attributes">Атрибуты для фильтра</param>
public sealed record DomainListSummaryDto(string Name, int Domains, int Ipv4, int Ipv6, int Skipped, IReadOnlyList<string> Attributes);
