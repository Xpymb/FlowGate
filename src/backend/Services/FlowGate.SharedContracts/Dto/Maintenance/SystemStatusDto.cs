namespace FlowGate.SharedContracts.Dto.Maintenance;

/// <summary>
///     Состояние FlowGate: версия, обновления, память
/// </summary>
/// <param name="Version">Версия FlowGate</param>
/// <param name="LatestVersion">Последняя стабильная версия по данным последней проверки</param>
/// <param name="UpdateAvailable">Доступна версия новее установленной</param>
/// <param name="UpdateCheckedAt">Время последней проверки обновлений</param>
/// <param name="PackageInstalled">FlowGate установлен пакетом opkg: самообновление доступно</param>
/// <param name="LastUpgradeResult">Итог последнего самообновления (<c>ok 0.2.0</c>, <c>failed …</c>) или null</param>
/// <param name="StartedAt">Время запуска FlowGate</param>
/// <param name="UptimeSeconds">Время работы, секунды</param>
/// <param name="MemoryRssKilobytes">Резидентная память FlowGate, КиБ (null вне Linux)</param>
/// <param name="XrayMemoryRssKilobytes">Резидентная память xray, КиБ (null, если xray не запущен)</param>
public sealed record SystemStatusDto(
    string Version,
    string? LatestVersion,
    bool UpdateAvailable,
    DateTimeOffset? UpdateCheckedAt,
    bool PackageInstalled,
    string? LastUpgradeResult,
    DateTimeOffset StartedAt,
    long UptimeSeconds,
    long? MemoryRssKilobytes,
    long? XrayMemoryRssKilobytes);
