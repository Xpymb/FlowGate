namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Предпросмотр списка с фильтром: что уйдёт в Keenetic
/// </summary>
/// <param name="Name">Имя списка</param>
/// <param name="AttributeFilter">Фильтр или null</param>
/// <param name="Total">Всего элементов</param>
/// <param name="Domains">Доменов</param>
/// <param name="Ipv4">IPv4-адресов и подсетей</param>
/// <param name="Ipv6">IPv6-адресов и подсетей</param>
/// <param name="SkippedKeyword">Пропущено <c>keyword:</c></param>
/// <param name="SkippedRegexp">Пропущено <c>regexp:</c></param>
/// <param name="SkippedInvalid">Отброшено некорректных значений</param>
/// <param name="Entries">Элементы (не больше <paramref name="Total"/>, при обрезке — первые)</param>
/// <param name="Truncated">Список элементов обрезан</param>
public sealed record DomainListPreviewDto(
    string Name,
    string? AttributeFilter,
    int Total,
    int Domains,
    int Ipv4,
    int Ipv6,
    int SkippedKeyword,
    int SkippedRegexp,
    int SkippedInvalid,
    IReadOnlyList<string> Entries,
    bool Truncated);
