using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Connections;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Keenetic;
using FlowGate.Contracts.Servers;
using FlowGate.Dal.Interfaces.Connections;
using FlowGate.Dal.Interfaces.DnsRoutes;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Exceptions;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Connections;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Соединения: хранение, конфигурация Xray и интерфейсы Keenetic. Изменения выполняются по одному, чтобы два
///     запроса не заняли один порт или интерфейс
/// </summary>
public sealed class ConnectionService : IConnectionService, IDisposable
{
    private const int MaxNameLength = 64;
    private const string EntityName = "Соединение";

    private readonly IConnectionProvider _connections;
    private readonly IServerProvider _servers;
    private readonly IDnsRouteProvider _routes;
    private readonly IKeeneticInterfaceService _interfaces;
    private readonly IWireguardKeyGenerator _keyGenerator;
    private readonly IXrayConfigService _xrayConfig;
    private readonly ConnectionOptions _options;
    private readonly KeeneticInterfaceParameters _interfaceParameters;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ConnectionService> _logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionService"/>
    /// </summary>
    /// <param name="connections">Хранилище соединений</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="routes">Хранилище DNS-маршрутов: соединение с маршрутами удалить нельзя</param>
    /// <param name="interfaces">Интерфейсы Keenetic</param>
    /// <param name="keyGenerator">Ключи WireGuard</param>
    /// <param name="xrayConfig">Применение конфигурации Xray</param>
    /// <param name="options">Настройки соединений</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public ConnectionService(
        IConnectionProvider connections,
        IServerProvider servers,
        IDnsRouteProvider routes,
        IKeeneticInterfaceService interfaces,
        IWireguardKeyGenerator keyGenerator,
        IXrayConfigService xrayConfig,
        IOptions<ConnectionOptions> options,
        TimeProvider timeProvider,
        ILogger<ConnectionService> logger)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _interfaces = interfaces ?? throw new ArgumentNullException(nameof(interfaces));
        _keyGenerator = keyGenerator ?? throw new ArgumentNullException(nameof(keyGenerator));
        _xrayConfig = xrayConfig ?? throw new ArgumentNullException(nameof(xrayConfig));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _interfaceParameters = new KeeneticInterfaceParameters(_options.ListenAddress, _options.WireguardMtu, _options.WireguardKeepaliveSeconds);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConnectionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var connections = await _connections.GetAllAsync(cancellationToken);
        if (connections.Count == 0)
        {
            return [];
        }

        var servers = await GetServersAsync(cancellationToken);
        var interfaces = await TryGetInterfacesAsync(cancellationToken);
        return connections.Select(connection => connection.ToDto(servers.GetValueOrDefault(connection.ServerId), interfaces)).ToList();
    }

    /// <inheritdoc />
    public async Task<ConnectionDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var connection = await GetExistingAsync(id, cancellationToken);
        return await ToDtoAsync(connection, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConnectionDto> CreateAsync(ConnectionCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kind = ParseKind(request.Kind);
        Connection connection;

        await _sync.WaitAsync(cancellationToken);
        try
        {
            var existing = await _connections.GetAllAsync(cancellationToken);
            var name = ValidateName(request.Name, existing, exceptId: null);
            await EnsureServerExistsAsync(request.ServerId, cancellationToken);

            var interfaces = await _interfaces.GetAllAsync(cancellationToken);
            connection = await AllocateAsync(name, kind, request.ServerId, existing, interfaces, cancellationToken);

            await _connections.AddAsync(connection, cancellationToken);
            try
            {
                await _xrayConfig.ApplyAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _connections.DeleteAsync(connection.Id, CancellationToken.None);
                throw;
            }

            try
            {
                await _interfaces.ExecuteAsync(
                    $"Создание интерфейса {connection.InterfaceName}",
                    KeeneticInterfaceCommands.Configure(connection, _interfaceParameters, bringUp: true),
                    ignoreMissing: false,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await RollbackCreatedAsync(connection);
                throw;
            }
        }
        finally
        {
            _sync.Release();
        }

        _logger.LogInformation(
            "Соединение {ConnectionId} «{Name}» создано: {Kind} через {Interface}, порт {Port}",
            connection.Id,
            connection.Name,
            connection.Kind,
            connection.InterfaceName,
            connection.Port);

        return await ToDtoAsync(connection, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ConnectionDto> UpdateAsync(string id, ConnectionUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Connection updated;
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var current = await GetExistingAsync(id, cancellationToken);
            var name = ValidateName(request.Name, await _connections.GetAllAsync(cancellationToken), exceptId: id);
            var serverChanged = !string.Equals(current.ServerId, request.ServerId, StringComparison.Ordinal);
            if (serverChanged)
            {
                await EnsureServerExistsAsync(request.ServerId, cancellationToken);
            }

            updated = current with { Name = name, ServerId = request.ServerId, UpdatedAt = _timeProvider.GetUtcNow() };
            await _connections.UpdateAsync(updated, cancellationToken);

            if (serverChanged)
            {
                try
                {
                    await _xrayConfig.ApplyAsync(cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    await _connections.UpdateAsync(current, CancellationToken.None);
                    throw;
                }
            }

            if (name != current.Name)
            {
                await _interfaces.ExecuteAsync(
                    $"Переименование интерфейса {updated.InterfaceName}",
                    [KeeneticInterfaceCommands.Describe(updated)],
                    ignoreMissing: false,
                    cancellationToken);
            }
        }
        finally
        {
            _sync.Release();
        }

        _logger.LogInformation("Соединение {ConnectionId} «{Name}» изменено, сервер {ServerId}", updated.Id, updated.Name, updated.ServerId);
        return await ToDtoAsync(updated, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        Connection connection;
        await _sync.WaitAsync(cancellationToken);
        try
        {
            connection = await GetExistingAsync(id, cancellationToken);
            var lists = (await _routes.GetAllAsync(cancellationToken))
                .Where(route => route.ConnectionId == id)
                .Select(route => $"«{route.ListName}»")
                .ToList();
            if (lists.Count > 0)
            {
                throw new ConflictException($"На соединение назначены маршруты {string.Join(", ", lists)}: сначала перенесите их на другое соединение");
            }

            await _interfaces.ExecuteAsync(
                $"Удаление интерфейса {connection.InterfaceName}",
                [KeeneticInterfaceCommands.Delete(connection.InterfaceName)],
                ignoreMissing: true,
                cancellationToken);

            await _connections.DeleteAsync(id, cancellationToken);
            await ApplyXrayQuietlyAsync(cancellationToken);
        }
        finally
        {
            _sync.Release();
        }

        _logger.LogInformation("Соединение {ConnectionId} «{Name}» удалено вместе с {Interface}", connection.Id, connection.Name, connection.InterfaceName);
    }

    /// <inheritdoc />
    public async Task<ConnectionReconcileResult> ReconcileAsync(CancellationToken cancellationToken)
    {
        await _sync.WaitAsync(cancellationToken);
        try
        {
            var connections = await _connections.GetAllAsync(cancellationToken);
            if (connections.Count == 0)
            {
                return ConnectionReconcileResult.None;
            }

            var interfaces = await _interfaces.GetAllAsync(cancellationToken);
            var commands = new List<string>();
            var recreated = new List<string>();
            var repaired = 0;

            foreach (var connection in connections)
            {
                if (!interfaces.TryGetValue(connection.InterfaceName, out var state))
                {
                    _logger.LogWarning("Интерфейс {Interface} соединения «{Name}» не найден в Keenetic: создаю заново", connection.InterfaceName, connection.Name);
                    commands.AddRange(KeeneticInterfaceCommands.Configure(connection, _interfaceParameters, bringUp: true));
                    recreated.Add(connection.InterfaceName);
                    repaired++;
                    continue;
                }

                if (!state.Description.StartsWith(Connection.DescriptionPrefix, StringComparison.Ordinal))
                {
                    _logger.LogWarning(
                        "Интерфейс {Interface} соединения «{Name}» занят чужим объектом ({Description}): не трогаю",
                        connection.InterfaceName,
                        connection.Name,
                        state.Description);
                    continue;
                }

                var drift = KeeneticInterfaceCommands.FindDrift(connection, state, _interfaceParameters);
                if (drift.Count > 0)
                {
                    _logger.LogWarning("Интерфейс {Interface} разошёлся с соединением «{Name}» ({Drift}): исправляю", connection.InterfaceName, connection.Name, string.Join(", ", drift));
                    commands.AddRange(KeeneticInterfaceCommands.Configure(connection, _interfaceParameters, bringUp: false));
                    repaired++;
                }
            }

            await _interfaces.ExecuteAsync("Сверка интерфейсов соединений", commands, ignoreMissing: false, cancellationToken);
            return new ConnectionReconcileResult(repaired, recreated);
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sync.Dispose();

    private async Task<Connection> AllocateAsync(
        string name,
        ConnectionKind kind,
        string serverId,
        IReadOnlyList<Connection> existing,
        IReadOnlyDictionary<string, KeeneticInterfaceState> interfaces,
        CancellationToken cancellationToken)
    {
        var port = ConnectionAllocator.NextPort(existing.Select(connection => connection.Port), _options.PortRangeStart, _options.PortRangeEnd)
            ?? throw new ConflictException($"Свободных портов в диапазоне {_options.PortRangeStart}–{_options.PortRangeEnd} не осталось");

        var interfaceName = ConnectionAllocator.NextInterfaceName(
            Connection.InterfacePrefix(kind),
            interfaces.Keys.Concat(existing.Select(connection => connection.InterfaceName)));

        WireguardSettings? wireguard = null;
        if (kind == ConnectionKind.Wireguard)
        {
            var address = ConnectionAllocator.NextWireguardAddress(
                    _options.WireguardSubnetPrefix,
                    existing.Select(connection => connection.Wireguard?.ClientAddress).OfType<string>())
                ?? throw new ConflictException($"Свободных адресов в подсети {_options.WireguardSubnetPrefix}.0/24 не осталось");

            var server = await _keyGenerator.GenerateAsync(cancellationToken);
            var client = await _keyGenerator.GenerateAsync(cancellationToken);
            wireguard = new WireguardSettings(server.PrivateKey, server.PublicKey, client.PrivateKey, client.PublicKey, address);
        }

        var now = _timeProvider.GetUtcNow();
        return new Connection(NewId(), name, kind, serverId, port, interfaceName, wireguard, now, now);
    }

    private async Task RollbackCreatedAsync(Connection connection)
    {
        try
        {
            await _interfaces.ExecuteAsync(
                $"Откат интерфейса {connection.InterfaceName}",
                [KeeneticInterfaceCommands.Delete(connection.InterfaceName)],
                ignoreMissing: true,
                CancellationToken.None);
        }
        catch (Exception ex) when (ex is KeeneticCommandException or KeeneticUnavailableException)
        {
            _logger.LogWarning("Не удалось удалить интерфейс {Interface} при откате: {Error}", connection.InterfaceName, ex.Message);
        }

        await _connections.DeleteAsync(connection.Id, CancellationToken.None);
        await ApplyXrayQuietlyAsync(CancellationToken.None);
    }

    private async Task ApplyXrayQuietlyAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _xrayConfig.ApplyAsync(cancellationToken);
        }
        catch (XrayOperationException ex)
        {
            _logger.LogWarning("Конфигурация Xray после изменения соединений не применена: {Error}", ex.Message);
        }
    }

    private async Task<ConnectionDto> ToDtoAsync(Connection connection, CancellationToken cancellationToken)
    {
        var server = await _servers.GetAsync(connection.ServerId, cancellationToken);
        var interfaces = await TryGetInterfacesAsync(cancellationToken);
        return connection.ToDto(server, interfaces);
    }

    private async Task<IReadOnlyDictionary<string, KeeneticInterfaceState>?> TryGetInterfacesAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _interfaces.GetAllAsync(cancellationToken);
        }
        catch (KeeneticUnavailableException ex)
        {
            _logger.LogWarning("Состояние интерфейсов Keenetic недоступно: {Error}", ex.Message);
            return null;
        }
    }

    private async Task<Dictionary<string, Server>> GetServersAsync(CancellationToken cancellationToken) =>
        (await _servers.GetAllAsync(cancellationToken)).ToDictionary(server => server.Id, StringComparer.Ordinal);

    private async Task EnsureServerExistsAsync(string? serverId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverId) || await _servers.GetAsync(serverId, cancellationToken) is null)
        {
            throw new RequestValidationException("Выберите существующий сервер");
        }
    }

    private async Task<Connection> GetExistingAsync(string id, CancellationToken cancellationToken) =>
        await _connections.GetAsync(id, cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

    private static string ValidateName(string? requested, IReadOnlyList<Connection> existing, string? exceptId)
    {
        var name = requested?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxNameLength)
        {
            throw new RequestValidationException($"Имя соединения обязательно, не длиннее {MaxNameLength} символов");
        }

        if (name.Any(character => character is '"' or '\\' || char.IsControl(character)))
        {
            throw new RequestValidationException("Имя соединения не может содержать кавычки, обратную косую черту и управляющие символы");
        }

        if (existing.Any(connection => connection.Id != exceptId && string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new RequestValidationException($"Соединение «{name}» уже есть");
        }

        return name;
    }

    private static ConnectionKind ParseKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "socks" or "socks5" => ConnectionKind.Socks,
            "http" => ConnectionKind.Http,
            "wireguard" or "wg" => ConnectionKind.Wireguard,
            _ => throw new RequestValidationException("Вид соединения: Socks, Http или Wireguard"),
        };

    private static string NewId() => "con-" + Guid.NewGuid().ToString("N")[..12];
}
