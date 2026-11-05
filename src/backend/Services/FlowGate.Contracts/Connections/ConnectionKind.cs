namespace FlowGate.Contracts.Connections;

/// <summary>
///     Вид соединения: протокол inbound Xray и тип интерфейса Keenetic
/// </summary>
public enum ConnectionKind
{
    /// <summary>
    ///     SOCKS5: inbound <c>socks</c> и интерфейс <c>ProxyN</c>. UDP через Keenetic не проходит
    /// </summary>
    Socks,

    /// <summary>
    ///     HTTP CONNECT: inbound <c>http</c> и интерфейс <c>ProxyN</c>. Только TCP
    /// </summary>
    Http,

    /// <summary>
    ///     WireGuard: inbound <c>wireguard</c> и интерфейс <c>WireguardN</c>. Переносит UDP, но нагружает роутер сильнее
    /// </summary>
    Wireguard,
}
