using System.Text.Json.Nodes;

using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Servers;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Готовые соединения и серверы для тестов. Ключи WireGuard совпадают с фикстурой Wireguard9
/// </summary>
public static class ConnectionTestData
{
    /// <summary>
    ///     Открытый ключ inbound Xray из фикстуры Wireguard9 (peer интерфейса)
    /// </summary>
    public const string FixtureServerPublicKey = "Kp5mU8LXHf4mVJ2EgkG+K2B9eFdDJzSpOIU6bMgItXo=";

    /// <summary>
    ///     Общие параметры интерфейсов, как в настройках по умолчанию
    /// </summary>
    public static KeeneticInterfaceParameters Parameters { get; } = new("127.0.0.1", 1420, 25);

    /// <summary>
    ///     Socks-соединение «YouTube» на Proxy9:10899
    /// </summary>
    public static Connection Socks { get; } = new(
        "con-socks", "YouTube", ConnectionKind.Socks, "srv-de", 10899, "Proxy9", null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    /// <summary>
    ///     WireGuard-соединение «Игры» на Wireguard9:10898 с адресом 10.255.0.99
    /// </summary>
    public static Connection Wireguard { get; } = new(
        "con-wg",
        "Игры",
        ConnectionKind.Wireguard,
        "srv-de",
        10898,
        "Wireguard9",
        new WireguardSettings("server-private", FixtureServerPublicKey, "client-private", "client-public", "10.255.0.99"),
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch);

    /// <summary>
    ///     Создаёт trojan-сервер
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <returns>Сервер</returns>
    public static Server Server(string id = "srv-de")
    {
        var outbound = new JsonObject
        {
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject { ["address"] = "de.example.net", ["port"] = 443, ["password"] = "p" },
        };
        return new Server(id, "Германия", outbound, ServerSourceKind.Manual, null, ServerImportKey.Compute(outbound), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
    }
}
