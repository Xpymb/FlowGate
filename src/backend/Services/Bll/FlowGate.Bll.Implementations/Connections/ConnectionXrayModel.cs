using System.Text.Json.Nodes;

using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Servers;
using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Модель конфигурации Xray из соединений: на соединение inbound <c>in-&lt;id&gt;</c>, outbound <c>out-&lt;id&gt;</c>
///     с копией outbound сервера и правило между ними. Если сервера нет, трафик соединения уходит в <c>block</c>,
///     чтобы не пойти напрямую
/// </summary>
public static class ConnectionXrayModel
{
    /// <summary>
    ///     Собирает модель
    /// </summary>
    /// <param name="logLevel">Уровень логирования Xray</param>
    /// <param name="connections">Соединения</param>
    /// <param name="servers">Серверы</param>
    /// <param name="listenAddress">Адрес inbound</param>
    /// <param name="wireguardMtu">MTU WireGuard-inbound</param>
    /// <returns>Модель конфигурации</returns>
    public static XrayConfigModel Build(
        string logLevel,
        IReadOnlyList<Connection> connections,
        IReadOnlyList<Server> servers,
        string listenAddress,
        int wireguardMtu)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(servers);

        var serversById = servers.ToDictionary(server => server.Id, StringComparer.Ordinal);
        var inbounds = new List<XrayInbound>();
        var outbounds = new List<XrayOutbound>();
        var rules = new List<XrayRoutingRule>();

        foreach (var connection in connections)
        {
            inbounds.Add(new XrayInbound(
                connection.InboundTag,
                Protocol(connection.Kind),
                listenAddress,
                connection.Port,
                InboundSettings(connection, wireguardMtu),
                Sniffing: true));

            if (serversById.TryGetValue(connection.ServerId, out var server))
            {
                outbounds.Add(new XrayOutbound(connection.OutboundTag, (JsonObject)server.Outbound.DeepClone()));
                rules.Add(new XrayRoutingRule([connection.InboundTag], connection.OutboundTag));
            }
            else
            {
                rules.Add(new XrayRoutingRule([connection.InboundTag], XrayConfigModel.BlockTag));
            }
        }

        return new XrayConfigModel(logLevel, inbounds, outbounds, rules);
    }

    private static string Protocol(ConnectionKind kind) =>
        kind switch
        {
            ConnectionKind.Socks => "socks",
            ConnectionKind.Http => "http",
            ConnectionKind.Wireguard => "wireguard",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид соединения"),
        };

    private static JsonObject? InboundSettings(Connection connection, int wireguardMtu)
    {
        switch (connection.Kind)
        {
            case ConnectionKind.Socks:
            {
                return new JsonObject { ["auth"] = "noauth", ["udp"] = true };
            }
            case ConnectionKind.Wireguard:
            {
                var wireguard = connection.Wireguard
                    ?? throw new InvalidOperationException($"У WireGuard-соединения {connection.Id} нет ключей");
                return new JsonObject
                {
                    ["secretKey"] = wireguard.ServerPrivateKey,
                    ["mtu"] = wireguardMtu,
                    ["peers"] = new JsonArray(new JsonObject
                    {
                        ["publicKey"] = wireguard.ClientPublicKey,
                        ["allowedIPs"] = new JsonArray($"{wireguard.ClientAddress}/32"),
                    }),
                };
            }
            default:
            {
                return null;
            }
        }
    }
}
