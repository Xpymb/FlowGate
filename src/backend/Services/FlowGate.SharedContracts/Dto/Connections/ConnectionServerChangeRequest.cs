namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Перевод нескольких соединений на один сервер
/// </summary>
/// <param name="ConnectionIds">Идентификаторы соединений</param>
/// <param name="ServerId">Сервер, через который пойдёт трафик</param>
public sealed record ConnectionServerChangeRequest(IReadOnlyList<string> ConnectionIds, string ServerId);
