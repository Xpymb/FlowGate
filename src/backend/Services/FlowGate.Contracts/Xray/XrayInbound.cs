using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Xray;

/// <summary>
///     Точка входа Xray (inbound)
/// </summary>
/// <param name="Tag">Уникальный тег</param>
/// <param name="Protocol">Протокол: <c>socks</c>, <c>http</c>, <c>wireguard</c></param>
/// <param name="Listen">Адрес прослушивания, обычно <c>127.0.0.1</c></param>
/// <param name="Port">Порт</param>
/// <param name="Settings">Объект <c>settings</c> протокола или null</param>
/// <param name="Sniffing">Включить sniffing (http, tls, quic)</param>
public sealed record XrayInbound(string Tag, string Protocol, string Listen, int Port, JsonObject? Settings, bool Sniffing);
