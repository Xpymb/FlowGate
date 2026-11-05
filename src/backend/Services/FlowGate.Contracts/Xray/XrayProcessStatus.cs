namespace FlowGate.Contracts.Xray;

/// <summary>
///     Снимок состояния процесса Xray
/// </summary>
/// <param name="State">Состояние</param>
/// <param name="ProcessId">Идентификатор процесса или null</param>
/// <param name="StartedAt">Время последнего успешного запуска</param>
/// <param name="Restarts">Сколько раз супервизор перезапускал процесс после падений</param>
/// <param name="LastExitCode">Код выхода последнего завершения</param>
/// <param name="LastError">Последняя ошибка запуска</param>
public sealed record XrayProcessStatus(
    XrayProcessState State,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int Restarts,
    int? LastExitCode,
    string? LastError);
