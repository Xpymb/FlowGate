using FlowGate.Contracts.Keenetic;

namespace FlowGate.Contracts.Connections;

/// <summary>
///     CLI-команды NDMS для интерфейса соединения (синтаксис проверен на KeeneticOS 5.2, docs/keenetic-findings.md §40–60)
/// </summary>
public static class KeeneticInterfaceCommands
{
    private const string AllowAllIps = "0.0.0.0 0.0.0.0";
    private const string HostMask = "255.255.255.255";

    /// <summary>
    ///     Команды, которые создают интерфейс или приводят существующий к нужным настройкам. Команды-установки
    ///     идемпотентны, поэтому годятся и для починки
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <param name="parameters">Общие параметры</param>
    /// <param name="bringUp">Включить интерфейс. Только при создании: выключенный пользователем интерфейс не включаем</param>
    /// <returns>Команды по порядку</returns>
    /// <exception cref="InvalidOperationException">Если у WireGuard-соединения нет ключей</exception>
    public static IReadOnlyList<string> Configure(Connection connection, KeeneticInterfaceParameters parameters, bool bringUp)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(parameters);

        var name = connection.InterfaceName;
        var commands = new List<string>
        {
            $"interface {name}",
            Describe(connection),
            $"interface {name} security-level public",
        };

        if (connection.Kind == ConnectionKind.Wireguard)
        {
            var wireguard = connection.Wireguard
                ?? throw new InvalidOperationException($"У WireGuard-соединения {connection.Id} нет ключей");
            var peer = $"interface {name} wireguard peer {wireguard.ServerPublicKey}";

            commands.Add($"interface {name} ip address {wireguard.ClientAddress} {HostMask}");
            commands.Add($"interface {name} ip mtu {parameters.WireguardMtu}");
            commands.Add($"interface {name} wireguard private-key {wireguard.ClientPrivateKey}");
            commands.Add(peer);
            commands.Add($"{peer} endpoint {parameters.UpstreamAddress}:{connection.Port}");
            commands.Add($"{peer} allow-ips {AllowAllIps}");
            commands.Add($"{peer} keepalive-interval {parameters.WireguardKeepaliveSeconds}");
        }
        else
        {
            commands.Add($"interface {name} proxy protocol {(connection.Kind == ConnectionKind.Socks ? "socks5" : "http")}");
            commands.Add($"interface {name} proxy upstream {parameters.UpstreamAddress} {connection.Port}");
            commands.Add($"interface {name} proxy connect");
        }

        if (bringUp)
        {
            commands.Add($"interface {name} up");
        }

        return commands;
    }

    /// <summary>
    ///     Команда, задающая описание интерфейса
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <returns>Команда</returns>
    public static string Describe(Connection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return $"interface {connection.InterfaceName} description \"{connection.InterfaceDescription}\"";
    }

    /// <summary>
    ///     Команда удаления интерфейса. Удаление несуществующего даёт <c>7405602 argument parse error</c>
    /// </summary>
    /// <param name="interfaceName">Имя интерфейса</param>
    /// <returns>Команда</returns>
    public static string Delete(string interfaceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interfaceName);
        return $"no interface {interfaceName}";
    }

    /// <summary>
    ///     Проверяет по видимым в <c>show interface</c> полям, расходится ли интерфейс с соединением.
    ///     Порт прокси-интерфейса RCI не показывает, поэтому для socks/http он не сверяется; адрес upstream сверяется
    ///     только у подключённого интерфейса — во время переподключения поле бывает другим
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <param name="state">Состояние интерфейса</param>
    /// <param name="parameters">Общие параметры</param>
    /// <returns>Описания расхождений; пусто — интерфейс в порядке</returns>
    public static IReadOnlyList<string> FindDrift(Connection connection, KeeneticInterfaceState state, KeeneticInterfaceParameters parameters)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(parameters);

        var drift = new List<string>();
        if (state.Description != connection.InterfaceDescription)
        {
            drift.Add("описание");
        }

        if (connection.Kind != ConnectionKind.Wireguard)
        {
            if (state.Connected
                && state.RemoteEndpointAddress is { Length: > 0 } remote
                && remote != parameters.UpstreamAddress)
            {
                drift.Add("адрес upstream");
            }

            return drift;
        }

        if (connection.Wireguard is { } wireguard)
        {
            if (state.Address != wireguard.ClientAddress)
            {
                drift.Add("адрес интерфейса");
            }

            var peer = state.WireguardPeers.FirstOrDefault(candidate => candidate.PublicKey == wireguard.ServerPublicKey);
            if (peer is null)
            {
                drift.Add("пир WireGuard");
            }
            else if (peer.RemotePort is not null && peer.RemotePort != connection.Port)
            {
                drift.Add("порт пира WireGuard");
            }
        }

        return drift;
    }
}
