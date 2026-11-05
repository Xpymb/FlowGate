namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Правило списка в формате v2fly domain-list-community
/// </summary>
/// <param name="Kind">Вид правила</param>
/// <param name="Value">Значение: домен, адрес, шаблон или имя вложенного списка</param>
/// <param name="Attributes">Атрибуты <c>@attr</c> в нижнем регистре</param>
public sealed record V2flyRule(V2flyRuleKind Kind, string Value, IReadOnlyList<string> Attributes)
{
    /// <summary>
    ///     Проверяет правило фильтром атрибута: <c>ads</c> — правило должно иметь атрибут, <c>!ads</c> — не должно
    ///     (семантика matchesAttr keenetic-geosite-sync и v2fly)
    /// </summary>
    /// <param name="filter">Фильтр или null — подходит любое правило</param>
    /// <returns>true, если правило подходит</returns>
    public bool Matches(string? filter) => MatchesAttributes(Attributes, filter);

    /// <summary>
    ///     Проверяет набор атрибутов фильтром
    /// </summary>
    /// <param name="attributes">Атрибуты правила</param>
    /// <param name="filter">Фильтр или null</param>
    /// <returns>true, если подходит</returns>
    public static bool MatchesAttributes(IReadOnlyList<string> attributes, string? filter)
    {
        ArgumentNullException.ThrowIfNull(attributes);

        if (string.IsNullOrEmpty(filter))
        {
            return true;
        }

        var negate = filter.StartsWith('!');
        var name = filter.TrimStart('!');
        var has = attributes.Contains(name, StringComparer.Ordinal);
        return negate ? !has : has;
    }
}
