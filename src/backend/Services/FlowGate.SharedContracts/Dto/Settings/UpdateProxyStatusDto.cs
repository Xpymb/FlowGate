namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Состояние прокси обновлений
/// </summary>
/// <param name="State"><c>Disabled</c>, <c>Idle</c> (запустится по запросу), <c>Running</c>, <c>Unavailable</c> (обновления идут напрямую)</param>
/// <param name="Port">Порт HTTP-inbound на 127.0.0.1</param>
/// <param name="ProcessId">Pid экземпляра Xray</param>
/// <param name="StartedAt">Когда запущен</param>
/// <param name="ActiveRequests">Запросов через прокси сейчас</param>
/// <param name="Reason">Почему недоступен или последний сбой</param>
public sealed record UpdateProxyStatusDto(
    string State,
    int Port,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int ActiveRequests,
    string? Reason);
