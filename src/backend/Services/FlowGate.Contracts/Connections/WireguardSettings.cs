namespace FlowGate.Contracts.Connections;

/// <summary>
///     Ключи и адрес WireGuard-соединения. Сервер — inbound Xray, клиент — интерфейс <c>WireguardN</c> Keenetic
/// </summary>
/// <param name="ServerPrivateKey">Закрытый ключ inbound Xray (<c>secretKey</c>)</param>
/// <param name="ServerPublicKey">Открытый ключ inbound Xray: peer интерфейса Keenetic</param>
/// <param name="ClientPrivateKey">Закрытый ключ интерфейса Keenetic</param>
/// <param name="ClientPublicKey">Открытый ключ интерфейса Keenetic: peer inbound Xray</param>
/// <param name="ClientAddress">Адрес интерфейса Keenetic (маска /32), например <c>10.255.0.2</c></param>
public sealed record WireguardSettings(
    string ServerPrivateKey,
    string ServerPublicKey,
    string ClientPrivateKey,
    string ClientPublicKey,
    string ClientAddress);
