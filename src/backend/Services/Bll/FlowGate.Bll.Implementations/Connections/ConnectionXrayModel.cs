using System.Text.Json.Nodes;

using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Servers;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Модель конфигурации Xray из соединений: на соединение inbound <c>in-&lt;id&gt;</c>, outbound <c>out-&lt;id&gt;</c>
///     с копией outbound сервера и правило между ними. У соединения с резервными серверами outbound на каждого
///     существующего кандидата (<c>out-&lt;id&gt;-&lt;слот&gt;</c>), балансировщик <c>bal-&lt;id&gt;</c> (<c>leastLoad</c>
///     с весами по приоритету) и общий <c>burstObservatory</c>. Если ни одного сервера нет, трафик соединения уходит в
///     <c>block</c>, чтобы не пойти напрямую
/// </summary>
public static class ConnectionXrayModel
{
    private static readonly double[] PriorityCosts = [1, 100, 10_000, 1_000_000];

    /// <summary>
    ///     Собирает модель
    /// </summary>
    /// <param name="logLevel">Уровень логирования Xray</param>
    /// <param name="connections">Соединения</param>
    /// <param name="servers">Серверы</param>
    /// <param name="options">Настройки соединений: адрес inbound, MTU WireGuard, проверки резервных серверов</param>
    /// <param name="apiPort">Порт API Xray на <c>127.0.0.1</c>: включается вместе с первым балансировщиком</param>
    /// <returns>Модель конфигурации</returns>
    public static XrayConfigModel Build(
        string logLevel,
        IReadOnlyList<Connection> connections,
        IReadOnlyList<Server> servers,
        ConnectionOptions options,
        int apiPort)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(servers);
        ArgumentNullException.ThrowIfNull(options);

        var serversById = servers.ToDictionary(server => server.Id, StringComparer.Ordinal);
        var inbounds = new List<XrayInbound>();
        var outbounds = new List<XrayOutbound>();
        var rules = new List<XrayRoutingRule>();
        var balancers = new List<XrayBalancer>();

        foreach (var connection in connections)
        {
            inbounds.Add(new XrayInbound(
                connection.InboundTag,
                Protocol(connection.Kind),
                options.ListenAddress,
                connection.Port,
                InboundSettings(connection, options.WireguardMtu),
                Sniffing: true));

            var candidates = connection.AliveCandidates(serversById)
                .Select(candidate => new XrayOutbound(candidate.OutboundTag, (JsonObject)candidate.Server.Outbound.DeepClone()))
                .ToList();

            outbounds.AddRange(candidates);

            switch (candidates.Count)
            {
                case 0:
                {
                    rules.Add(new XrayRoutingRule([connection.InboundTag], XrayConfigModel.BlockTag));
                    break;
                }
                case 1:
                {
                    rules.Add(new XrayRoutingRule([connection.InboundTag], candidates[0].Tag));
                    break;
                }
                default:
                {
                    var tags = candidates.Select(candidate => candidate.Tag).ToList();
                    balancers.Add(new XrayBalancer(
                        connection.BalancerTag,
                        tags,
                        tags[0],
                        tags.Select((tag, rank) => (tag, rank)).ToDictionary(item => item.tag, item => PriorityCosts[Math.Min(item.rank, PriorityCosts.Length - 1)], StringComparer.Ordinal),
                        options.FallbackTolerance));
                    rules.Add(XrayRoutingRule.ToBalancer([connection.InboundTag], connection.BalancerTag));
                    break;
                }
            }
        }

        var observatory = balancers.Count == 0
            ? null
            : new XrayObservatory(
                balancers.SelectMany(balancer => balancer.Selector).ToList(),
                options.FallbackProbeUrl,
                $"{options.FallbackProbeIntervalSeconds}s",
                options.FallbackProbeSampling,
                $"{options.FallbackProbeTimeoutSeconds}s");

        return new XrayConfigModel(logLevel, inbounds, outbounds, rules)
        {
            Balancers = balancers,
            Observatory = observatory,
            Api = balancers.Count == 0 ? null : new XrayApi($"127.0.0.1:{apiPort}", ["RoutingService"]),
        };
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
