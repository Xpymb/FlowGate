namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Вид правила списка v2fly
/// </summary>
public enum V2flyRuleKind
{
    /// <summary>
    ///     <c>domain:</c> или строка без префикса — домен с поддоменами
    /// </summary>
    Domain,

    /// <summary>
    ///     <c>full:</c> — ровно этот домен. Keenetic отдельного режима не имеет, уходит как домен
    /// </summary>
    Full,

    /// <summary>
    ///     <c>keyword:</c> — подстрока; Keenetic не поддерживает, пропускается
    /// </summary>
    Keyword,

    /// <summary>
    ///     <c>regexp:</c> — регулярное выражение; Keenetic не поддерживает, пропускается
    /// </summary>
    Regexp,

    /// <summary>
    ///     <c>include:</c> — вложенный список того же источника
    /// </summary>
    Include,

    /// <summary>
    ///     Адрес или подсеть IPv4/IPv6 (расширение FlowGate)
    /// </summary>
    Address,
}
