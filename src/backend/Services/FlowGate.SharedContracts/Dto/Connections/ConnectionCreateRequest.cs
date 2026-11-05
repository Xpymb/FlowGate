namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Создание соединения
/// </summary>
/// <param name="Name">Имя, до 64 символов, без кавычек</param>
/// <param name="Kind"><c>Socks</c>, <c>Http</c> или <c>Wireguard</c></param>
/// <param name="ServerId">Сервер, через который пойдёт трафик</param>
public sealed record ConnectionCreateRequest(string Name, string Kind, string ServerId);
