namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Состояние интерфейса Keenetic соединения
/// </summary>
/// <param name="Exists">Интерфейс есть в Keenetic</param>
/// <param name="Owned">Интерфейс принадлежит FlowGate (описание начинается с <c>FlowGate: </c>)</param>
/// <param name="State">Административное состояние: <c>up</c> или <c>down</c></param>
/// <param name="Link">Состояние канала: <c>up</c> или <c>down</c></param>
/// <param name="Connected">Интерфейс подключён</param>
/// <param name="WireguardOnline">Для WireGuard: рукопожатие с Xray было недавно</param>
/// <param name="LastHandshakeSecondsAgo">Для WireGuard: сколько секунд назад было рукопожатие, null — ни разу</param>
public sealed record ConnectionInterfaceDto(
    bool Exists,
    bool Owned,
    string? State,
    string? Link,
    bool Connected,
    bool? WireguardOnline,
    long? LastHandshakeSecondsAgo);
