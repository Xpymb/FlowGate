namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Соединение с состоянием интерфейса Keenetic
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя</param>
/// <param name="Kind"><c>Socks</c>, <c>Http</c> или <c>Wireguard</c></param>
/// <param name="ServerId">Сервер</param>
/// <param name="ServerName">Имя сервера или null, если сервер удалён</param>
/// <param name="Port">Порт inbound Xray</param>
/// <param name="InterfaceName">Интерфейс Keenetic</param>
/// <param name="SupportsUdp">Переносит UDP (QUIC, звонки); socks и http через Keenetic — только TCP</param>
/// <param name="Interface">Состояние интерфейса или null, если Keenetic не ответил</param>
/// <param name="Problems">Что мешает соединению работать, для показа пользователю</param>
/// <param name="CreatedAt">Когда создано</param>
/// <param name="UpdatedAt">Когда последний раз изменено</param>
public sealed record ConnectionDto(
    string Id,
    string Name,
    string Kind,
    string ServerId,
    string? ServerName,
    int Port,
    string InterfaceName,
    bool SupportsUdp,
    ConnectionInterfaceDto? Interface,
    IReadOnlyList<string> Problems,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
