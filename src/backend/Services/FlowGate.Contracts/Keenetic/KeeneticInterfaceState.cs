namespace FlowGate.Contracts.Keenetic;

/// <summary>
///     Состояние интерфейса Keenetic по ответу RCI <c>show/interface</c>
/// </summary>
/// <param name="Name">Имя интерфейса, например <c>Proxy0</c></param>
/// <param name="Type">Тип: <c>Proxy</c>, <c>Wireguard</c> и т.д.</param>
/// <param name="Description">Описание</param>
/// <param name="State">Административное состояние: <c>up</c> или <c>down</c></param>
/// <param name="Link">Состояние канала: <c>up</c> или <c>down</c></param>
/// <param name="Connected">Интерфейс подключён</param>
/// <param name="Address">IPv4-адрес интерфейса или null</param>
/// <param name="RemoteEndpointAddress">Адрес, к которому подключается прокси-интерфейс, или null</param>
/// <param name="WireguardPeers">Пиры WireGuard; для остальных типов пусто</param>
public sealed record KeeneticInterfaceState(
    string Name,
    string Type,
    string Description,
    string State,
    string Link,
    bool Connected,
    string? Address,
    string? RemoteEndpointAddress,
    IReadOnlyList<KeeneticWireguardPeer> WireguardPeers);
