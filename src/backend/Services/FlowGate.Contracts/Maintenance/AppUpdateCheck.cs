namespace FlowGate.Contracts.Maintenance;

/// <summary>
///     Результат проверки новых версий FlowGate
/// </summary>
/// <param name="LatestVersion">Последняя стабильная версия или null, если подходящих релизов нет</param>
/// <param name="CheckedAt">Время проверки</param>
public sealed record AppUpdateCheck(Version? LatestVersion, DateTimeOffset CheckedAt)
{
    /// <summary>
    ///     Разбирает версию FlowGate из тега релиза (<c>v0.2.0</c>) или строки версии (<c>0.2.0</c>)
    /// </summary>
    /// <param name="value">Тег или версия</param>
    /// <returns>Версия из трёх чисел или null, если формат другой</returns>
    public static Version? ParseVersion(string? value)
    {
        var token = value?.Trim().TrimStart('v', 'V');
        return Version.TryParse(token, out var version) && version.Build >= 0 && version.Revision < 0 ? version : null;
    }

    /// <summary>
    ///     Выбирает последнюю стабильную версию из релизов: черновики, pre-release и теги не вида <c>vX.Y.Z</c> пропускаются
    /// </summary>
    /// <param name="releases">Релизы: тег, черновик, pre-release</param>
    /// <param name="checkedAt">Время проверки</param>
    /// <returns>Результат проверки</returns>
    /// <exception cref="ArgumentNullException">Если releases равен null</exception>
    public static AppUpdateCheck FromReleases(IEnumerable<(string Tag, bool Draft, bool Prerelease)> releases, DateTimeOffset checkedAt)
    {
        ArgumentNullException.ThrowIfNull(releases);

        var latest = releases
            .Where(release => !release.Draft && !release.Prerelease)
            .Select(release => ParseVersion(release.Tag))
            .OfType<Version>()
            .Max();
        return new AppUpdateCheck(latest, checkedAt);
    }

    /// <summary>
    ///     Есть ли версия новее указанной
    /// </summary>
    /// <param name="current">Текущая версия</param>
    /// <returns>true, если последняя версия новее</returns>
    public bool IsNewerThan(Version? current) => LatestVersion is not null && (current is null || LatestVersion > current);
}
