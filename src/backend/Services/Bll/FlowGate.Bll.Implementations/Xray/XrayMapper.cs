using FlowGate.Contracts.Xray;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Маппинг сущностей Xray в DTO
/// </summary>
public static class XrayMapper
{
    /// <summary>
    ///     Преобразует релиз в DTO
    /// </summary>
    /// <param name="release">Релиз</param>
    /// <param name="installed">Установленная версия или null</param>
    /// <param name="latestStable">Последняя стабильная версия или null</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если release равен null</exception>
    public static XrayReleaseDto ToDto(this XrayRelease release, XrayVersion? installed, XrayVersion? latestStable)
    {
        ArgumentNullException.ThrowIfNull(release);

        return new XrayReleaseDto(
            release.Version.ToString(),
            release.Tag,
            release.PublishedAt,
            release.IsPrerelease,
            release.ArchiveSize,
            release.Version == installed,
            release.Version == latestStable);
    }

    /// <summary>
    ///     Собирает DTO состояния
    /// </summary>
    /// <param name="status">Состояние процесса</param>
    /// <param name="installed">Бинарник установлен</param>
    /// <param name="version">Установленная версия или null</param>
    /// <param name="autostart">Автозапуск включён</param>
    /// <param name="lastCheck">Последняя проверка обновлений или null</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если status равен null</exception>
    public static XrayStatusDto ToDto(
        this XrayProcessStatus status,
        bool installed,
        XrayVersion? version,
        bool autostart,
        XrayUpdateCheck? lastCheck)
    {
        ArgumentNullException.ThrowIfNull(status);

        var latest = lastCheck?.LatestVersion;
        return new XrayStatusDto(
            installed,
            version?.ToString(),
            status.State.ToString(),
            status.ProcessId,
            status.StartedAt,
            status.Restarts,
            status.LastExitCode,
            status.LastError,
            autostart,
            latest?.ToString(),
            installed && latest is not null && (version is null || latest > version),
            lastCheck?.CheckedAt);
    }

    /// <summary>
    ///     Преобразует строку вывода в DTO
    /// </summary>
    /// <param name="line">Строка вывода</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если line равен null</exception>
    public static XrayLogLineDto ToDto(this XrayLogLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return new XrayLogLineDto(line.Timestamp, line.Text);
    }
}
