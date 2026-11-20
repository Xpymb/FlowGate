using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Keenetic;
using FlowGate.Contracts.Servers;
using FlowGate.Contracts.Xray;
using FlowGate.SharedContracts.Dto.Connections;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Маппинг соединений в DTO
/// </summary>
public static class ConnectionMapper
{
    /// <summary>
    ///     Преобразует соединение в DTO с состоянием интерфейса и списком проблем
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <param name="servers">Серверы по идентификатору: основной и резервные ищутся в них</param>
    /// <param name="interfaces">Интерфейсы Keenetic или null, если Keenetic не ответил</param>
    /// <param name="balancers">Выбор балансировщиков Xray</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если connection равен null</exception>
    public static ConnectionDto ToDto(
        this Connection connection,
        IReadOnlyDictionary<string, Server> servers,
        IReadOnlyDictionary<string, KeeneticInterfaceState>? interfaces,
        XrayBalancerStatus balancers)
    {
        ArgumentNullException.ThrowIfNull(balancers);
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(servers);

        var server = servers.GetValueOrDefault(connection.ServerId);
        var fallbacks = connection.FallbackServerIds
            .Select(serverId => new ConnectionFallbackDto(serverId, servers.GetValueOrDefault(serverId)?.Name))
            .ToList();
        var aliveFallbacks = fallbacks.Count(fallback => fallback.ServerName is not null);

        var problems = new List<string>();
        if (server is null)
        {
            problems.Add(aliveFallbacks > 0
                ? "Основной сервер удалён: трафик идёт через резервные, выберите другой основной сервер"
                : "Сервер удалён: трафик соединения блокируется, выберите другой сервер");
        }

        var missingFallbacks = fallbacks.Count - aliveFallbacks;
        if (missingFallbacks > 0)
        {
            problems.Add($"Удалено резервных серверов: {missingFallbacks}");
        }

        ConnectionInterfaceDto? interfaceDto = null;
        if (interfaces is null)
        {
            problems.Add("Keenetic не ответил: состояние интерфейса неизвестно");
        }
        else if (!interfaces.TryGetValue(connection.InterfaceName, out var state))
        {
            interfaceDto = new ConnectionInterfaceDto(false, false, null, null, false, null, null);
            problems.Add($"Интерфейса {connection.InterfaceName} нет в Keenetic: он будет создан заново при сверке");
        }
        else
        {
            var owned = state.Description.StartsWith(Connection.DescriptionPrefix, StringComparison.Ordinal);
            var peer = connection.Wireguard is { } wireguard
                ? state.WireguardPeers.FirstOrDefault(candidate => candidate.PublicKey == wireguard.ServerPublicKey)
                : null;

            interfaceDto = new ConnectionInterfaceDto(
                true,
                owned,
                state.State,
                state.Link,
                state.Connected,
                connection.Kind == ConnectionKind.Wireguard ? peer?.Online ?? false : null,
                peer?.LastHandshakeSecondsAgo);

            if (!owned)
            {
                problems.Add($"Интерфейс {connection.InterfaceName} в Keenetic не принадлежит FlowGate");
            }
            else if (!string.Equals(state.State, "up", StringComparison.OrdinalIgnoreCase))
            {
                problems.Add($"Интерфейс {connection.InterfaceName} выключен в Keenetic");
            }
        }

        return new ConnectionDto(
            connection.Id,
            connection.Name,
            connection.Kind.ToString(),
            connection.ServerId,
            server?.Name,
            fallbacks,
            Routing(connection, servers, balancers),
            connection.Port,
            connection.InterfaceName,
            connection.SupportsUdp,
            interfaceDto,
            problems,
            connection.CreatedAt,
            connection.UpdatedAt);
    }

    private static ConnectionRoutingDto Routing(
        Connection connection,
        IReadOnlyDictionary<string, Server> servers,
        XrayBalancerStatus balancers)
    {
        var candidates = connection.AliveCandidates(servers);
        switch (candidates.Count)
        {
            case 0:
            {
                return new ConnectionRoutingDto("Blocked", null, null, null);
            }
            case 1:
            {
                return Active("Direct", candidates[0]);
            }
        }

        if (balancers.Support == XrayBalancerSupport.Unsupported)
        {
            return new ConnectionRoutingDto("Unsupported", null, null, null);
        }

        if (balancers.Support != XrayBalancerSupport.Supported
            || !balancers.ActiveTags.TryGetValue(connection.BalancerTag, out var activeTag))
        {
            return new ConnectionRoutingDto("Unavailable", null, null, null);
        }

        var active = activeTag is null
            ? candidates[0]
            : candidates.FirstOrDefault(candidate => string.Equals(candidate.OutboundTag, activeTag, StringComparison.Ordinal));
        return active is null ? new ConnectionRoutingDto("Unavailable", null, null, null) : Active("Active", active);
    }

    private static ConnectionRoutingDto Active(string status, ConnectionCandidate candidate) =>
        new(status, candidate.Server.Id, candidate.Server.Name, candidate.Slot);
}
