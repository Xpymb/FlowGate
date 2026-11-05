namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Готовое содержимое списка для Keenetic: нормализованные элементы без дубликатов в стабильном порядке
/// </summary>
/// <param name="Entries">Домены, адреса и подсети</param>
/// <param name="SkippedKeyword">Сколько правил <c>keyword:</c> пропущено (Keenetic их не поддерживает)</param>
/// <param name="SkippedRegexp">Сколько правил <c>regexp:</c> пропущено</param>
/// <param name="SkippedInvalid">Сколько значений отброшено как некорректные</param>
public sealed record ListContent(IReadOnlyList<string> Entries, int SkippedKeyword, int SkippedRegexp, int SkippedInvalid)
{
    /// <summary>
    ///     Пустой список
    /// </summary>
    public static ListContent Empty { get; } = new([], 0, 0, 0);

    /// <summary>
    ///     Всего пропущено правил
    /// </summary>
    public int Skipped => SkippedKeyword + SkippedRegexp + SkippedInvalid;

    /// <summary>
    ///     Считает элементы по видам
    /// </summary>
    /// <returns>Число доменов, IPv4 и IPv6</returns>
    public (int Domains, int Ipv4, int Ipv6) CountByKind()
    {
        int domains = 0, ipv4 = 0, ipv6 = 0;
        foreach (var entry in Entries)
        {
            switch (ListEntries.KindOf(entry))
            {
                case ListEntryKind.Ipv4:
                {
                    ipv4++;
                    break;
                }
                case ListEntryKind.Ipv6:
                {
                    ipv6++;
                    break;
                }
                default:
                {
                    domains++;
                    break;
                }
            }
        }

        return (domains, ipv4, ipv6);
    }
}
