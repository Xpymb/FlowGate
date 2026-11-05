namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Вид источника списков
/// </summary>
public enum DomainSourceKind
{
    /// <summary>
    ///     Публичный GitHub-репозиторий в текстовом формате v2fly: папка <c>data/</c>, файл — список
    /// </summary>
    GitHubRepository,

    /// <summary>
    ///     Собранный <c>geosite.dat</c> (v2fly <c>dlc.dat</c> и совместимые): домены
    /// </summary>
    GeositeDat,

    /// <summary>
    ///     Собранный <c>geoip.dat</c>: подсети
    /// </summary>
    GeoipDat,

    /// <summary>
    ///     Списки, которые правятся прямо в FlowGate (тот же формат v2fly)
    /// </summary>
    Local,
}
