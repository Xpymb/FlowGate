namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Резервный сервер соединения
/// </summary>
/// <param name="ServerId">Сервер</param>
/// <param name="ServerName">Имя сервера или null, если сервер удалён</param>
public sealed record ConnectionFallbackDto(string ServerId, string? ServerName);
