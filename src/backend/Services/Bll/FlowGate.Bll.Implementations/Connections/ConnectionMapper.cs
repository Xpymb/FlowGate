using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Keenetic;
using FlowGate.Contracts.Servers;
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
    /// <param name="server">Сервер соединения или null, если он удалён</param>
    /// <param name="interfaces">Интерфейсы Keenetic или null, если Keenetic не ответил</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если connection равен null</exception>
    public static ConnectionDto ToDto(
        this Connection connection,
        Server? server,
        IReadOnlyDictionary<string, KeeneticInterfaceState>? interfaces)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var problems = new List<string>();
        if (server is null)
        {
            problems.Add("Сервер удалён: трафик соединения блокируется, выберите другой сервер");
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
            connection.Port,
            connection.InterfaceName,
            connection.SupportsUdp,
            interfaceDto,
            problems,
            connection.CreatedAt,
            connection.UpdatedAt);
    }
}
