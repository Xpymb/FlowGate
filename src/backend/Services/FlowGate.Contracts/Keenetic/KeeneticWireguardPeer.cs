namespace FlowGate.Contracts.Keenetic;

/// <summary>
///     Пир WireGuard-интерфейса Keenetic
/// </summary>
/// <param name="PublicKey">Открытый ключ пира</param>
/// <param name="RemoteAddress">Адрес пира или null</param>
/// <param name="RemotePort">Порт пира или null</param>
/// <param name="Online">Рукопожатие было недавно</param>
/// <param name="LastHandshakeSecondsAgo">Сколько секунд назад было рукопожатие; null — ни разу</param>
public sealed record KeeneticWireguardPeer(
    string PublicKey,
    string? RemoteAddress,
    int? RemotePort,
    bool Online,
    long? LastHandshakeSecondsAgo);
