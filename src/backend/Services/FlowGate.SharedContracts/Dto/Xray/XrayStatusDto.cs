namespace FlowGate.SharedContracts.Dto.Xray;

/// <summary>
///     Состояние Xray на роутере
/// </summary>
/// <param name="Installed">Бинарник установлен</param>
/// <param name="Version">Установленная версия или null</param>
/// <param name="State">Состояние процесса: <c>NotInstalled</c>, <c>Stopped</c>, <c>Starting</c>, <c>Running</c>, <c>Restarting</c>, <c>Failed</c></param>
/// <param name="ProcessId">Идентификатор процесса или null</param>
/// <param name="StartedAt">Время последнего успешного запуска</param>
/// <param name="Restarts">Сколько раз Xray перезапускали после падений</param>
/// <param name="LastExitCode">Код выхода последнего завершения</param>
/// <param name="LastError">Последняя ошибка запуска</param>
/// <param name="Autostart">Запускать Xray при старте FlowGate</param>
/// <param name="LatestVersion">Последняя стабильная версия по данным последней проверки</param>
/// <param name="UpdateAvailable">Доступна версия новее установленной</param>
/// <param name="UpdateCheckedAt">Время последней проверки обновлений</param>
public sealed record XrayStatusDto(
    bool Installed,
    string? Version,
    string State,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int Restarts,
    int? LastExitCode,
    string? LastError,
    bool Autostart,
    string? LatestVersion,
    bool UpdateAvailable,
    DateTimeOffset? UpdateCheckedAt);
