using System.Text.Json;
using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Servers;

/// <summary>
///     Сводка outbound для списка серверов и ключа идентичности
/// </summary>
/// <param name="Protocol">Протокол Xray: <c>vless</c>, <c>vmess</c>, <c>trojan</c>, <c>shadowsocks</c>, <c>hysteria</c> …</param>
/// <param name="Address">Адрес сервера</param>
/// <param name="Port">Порт или null</param>
/// <param name="Network">Транспорт: <c>raw</c>, <c>ws</c>, <c>grpc</c>, <c>xhttp</c> …</param>
/// <param name="Security">Защита: <c>none</c>, <c>tls</c>, <c>reality</c></param>
/// <param name="ServerName">SNI или serverName REALITY</param>
/// <param name="Path">Путь транспорта (ws, httpupgrade, xhttp)</param>
/// <param name="Mode">Режим xhttp</param>
/// <param name="ServiceName">serviceName gRPC</param>
public sealed record OutboundSummary(
    string Protocol,
    string Address,
    int? Port,
    string Network,
    string Security,
    string ServerName,
    string Path,
    string Mode,
    string ServiceName)
{
    /// <summary>
    ///     Извлекает сводку из outbound. Поддерживает плоский <c>settings</c> (Xray 26), <c>vnext[0]</c> и <c>servers[0]</c>
    /// </summary>
    /// <param name="outbound">Outbound Xray</param>
    /// <returns>Сводка; отсутствующие поля — пустые строки</returns>
    /// <exception cref="ArgumentNullException">Если outbound равен null</exception>
    public static OutboundSummary From(JsonObject outbound)
    {
        ArgumentNullException.ThrowIfNull(outbound);

        var settings = outbound["settings"] as JsonObject;
        var endpoint = FirstItem(settings, "vnext") ?? FirstItem(settings, "servers") ?? FirstItem(settings, "peers") ?? settings;
        var stream = outbound["streamSettings"] as JsonObject;
        var network = Text(stream, "network");
        var transport = network.Length == 0 ? null : stream?[TransportSettingsName(network)] as JsonObject;

        return new OutboundSummary(
            Text(outbound, "protocol"),
            Text(endpoint, "address").Length > 0 ? Text(endpoint, "address") : Text(endpoint, "endpoint"),
            Number(endpoint, "port"),
            network.Length == 0 ? "raw" : network,
            Text(stream, "security").Length == 0 ? "none" : Text(stream, "security"),
            FirstNonEmpty(Text(stream?["realitySettings"] as JsonObject, "serverName"), Text(stream?["tlsSettings"] as JsonObject, "serverName")),
            Text(transport, "path"),
            Text(transport, "mode"),
            Text(transport, "serviceName"));
    }

    private static string TransportSettingsName(string network) =>
        network switch
        {
            "tcp" => "tcpSettings",
            "splithttp" => "splithttpSettings",
            _ => network + "Settings",
        };

    private static JsonObject? FirstItem(JsonObject? settings, string name) =>
        settings?[name] is JsonArray { Count: > 0 } array ? array[0] as JsonObject : null;

    private static string Text(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : string.Empty;

    private static int? Number(JsonObject? node, string name) =>
        node?[name] is JsonValue value && value.GetValueKind() == JsonValueKind.Number && value.TryGetValue<int>(out var number)
            ? number
            : null;

    private static string FirstNonEmpty(string first, string second) => first.Length > 0 ? first : second;
}
