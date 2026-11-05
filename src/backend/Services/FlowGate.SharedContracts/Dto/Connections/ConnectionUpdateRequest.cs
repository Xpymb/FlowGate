namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Изменение соединения. Вид соединения не меняется: для этого создайте новое
/// </summary>
/// <param name="Name">Имя, до 64 символов, без кавычек</param>
/// <param name="ServerId">Сервер, через который пойдёт трафик</param>
public sealed record ConnectionUpdateRequest(string Name, string ServerId);
