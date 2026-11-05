namespace FlowGate.Contracts.Connections;

/// <summary>
///     Пара ключей WireGuard (Curve25519, base64)
/// </summary>
/// <param name="PrivateKey">Закрытый ключ</param>
/// <param name="PublicKey">Открытый ключ</param>
public sealed record WireguardKeyPair(string PrivateKey, string PublicKey);
