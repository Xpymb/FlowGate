namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Результат пинга сервера: время HTTP-запроса через outbound вместе с установкой соединения
/// </summary>
/// <param name="ServerId">Сервер</param>
/// <param name="LatencyMs">Задержка в миллисекундах или null, если сервер не ответил</param>
/// <param name="Error">Почему сервер не ответил; null при успехе</param>
public sealed record ServerPingResultDto(string ServerId, int? LatencyMs, string? Error);
