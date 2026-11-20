using System.ComponentModel;
using System.Net;
using System.Net.Sockets;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Servers;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Пинг серверов через временный экземпляр Xray (<see cref="PingXrayModel"/>): процесс живёт, пока идут запросы, и
///     убивается в конце; запускается через <see cref="IXrayProcessManager"/>, чтобы при остановке FlowGate не остаться
/// </summary>
public sealed class ServerPingService : IServerPingService, IDisposable
{
    private const int OutputTailLines = 5;
    private static readonly TimeSpan PortPollInterval = TimeSpan.FromMilliseconds(100);

    private readonly IServerProvider _servers;
    private readonly IXrayVersionProvider _versionProvider;
    private readonly IXrayProcessManager _processManager;
    private readonly IXrayConfigBuilder _builder;
    private readonly IOutboundProbe _probe;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerPingService> _logger;
    private readonly PingOptions _options;
    private readonly Uri _url;
    private readonly string _workDirectory;
    private readonly SemaphoreSlim _sync = new(1, 1);

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerPingService"/>
    /// </summary>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="versionProvider">Установлен ли Xray</param>
    /// <param name="processManager">Запуск временного Xray</param>
    /// <param name="builder">Сборка конфигурации Xray</param>
    /// <param name="probe">HTTP-запрос через socks-inbound</param>
    /// <param name="options">Настройки пинга</param>
    /// <param name="xrayOptions">Настройки Xray (рабочий каталог)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public ServerPingService(
        IServerProvider servers,
        IXrayVersionProvider versionProvider,
        IXrayProcessManager processManager,
        IXrayConfigBuilder builder,
        IOutboundProbe probe,
        IOptions<PingOptions> options,
        IOptions<XrayOptions> xrayOptions,
        TimeProvider timeProvider,
        ILogger<ServerPingService> logger)
    {
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(xrayOptions);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options.Value;
        _url = new Uri(_options.Url, UriKind.Absolute);
        _workDirectory = Path.GetFullPath(xrayOptions.Value.WorkDirectory);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerPingResultDto>> PingAsync(ServerPingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var ids = (request.ServerIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count == 0)
        {
            throw new RequestValidationException("Выберите серверы для пинга");
        }

        if (ids.Count > _options.MaxServersPerRequest)
        {
            throw new RequestValidationException($"За один раз можно пинговать не больше {_options.MaxServersPerRequest} серверов");
        }

        if (!_versionProvider.IsInstalled)
        {
            throw new ConflictException("Xray не установлен: пинг серверов недоступен");
        }

        var servers = (await _servers.GetAllAsync(cancellationToken)).ToDictionary(server => server.Id, StringComparer.Ordinal);
        var found = ids.Where(servers.ContainsKey).Select(id => servers[id]).ToList();
        var results = new Dictionary<string, ServerPingResultDto>(StringComparer.Ordinal);
        foreach (var missing in ids.Where(id => !servers.ContainsKey(id)))
        {
            results[missing] = new ServerPingResultDto(missing, null, "Сервер не найден");
        }

        if (found.Count > 0)
        {
            await _sync.WaitAsync(cancellationToken);
            try
            {
                foreach (var result in await PingFoundAsync(found, cancellationToken))
                {
                    results[result.ServerId] = result;
                }
            }
            finally
            {
                _sync.Release();
            }
        }

        return ids.Select(id => results[id]).ToList();
    }

    /// <inheritdoc />
    public void Dispose() => _sync.Dispose();

    private async Task<IReadOnlyList<ServerPingResultDto>> PingFoundAsync(IReadOnlyList<Server> servers, CancellationToken cancellationToken)
    {
        var ports = AllocatePorts(servers.Count);
        var targets = servers.Select((server, index) => (server.Outbound, ports[index])).ToList();
        var config = _builder.Build(PingXrayModel.Build(targets));

        Directory.CreateDirectory(_workDirectory);
        var configPath = Path.Combine(_workDirectory, $"ping-{Guid.NewGuid():N}.json");
        var output = new Queue<string>();
        IManagedProcess? process = null;
        try
        {
            await File.WriteAllTextAsync(configPath, config.ToJsonString(), cancellationToken);
            try
            {
                process = _processManager.Start(["run", "-c", configPath], line => Remember(output, line));
            }
            catch (Exception ex) when (ex is Win32Exception or IOException)
            {
                return Fail(servers, $"Не удалось запустить Xray: {ex.Message}");
            }

            if (!await WaitForPortsAsync(process, ports, cancellationToken))
            {
                var reason = Tail(output);
                _logger.LogWarning("Temporary Xray for ping failed to start: {Reason}", reason);
                return Fail(servers, $"Xray не запустился: {reason}");
            }

            var results = new ServerPingResultDto[servers.Count];
            var parallel = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxConcurrency, CancellationToken = cancellationToken };
            await Parallel.ForEachAsync(
                Enumerable.Range(0, servers.Count),
                parallel,
                async (index, token) => results[index] = await ProbeAsync(servers[index], ports[index], token));

            _logger.LogInformation(
                "Server ping: {Alive} of {Total} responded",
                results.Count(result => result.LatencyMs is not null),
                results.Length);
            return results;
        }
        finally
        {
            if (process is not null)
            {
                process.Kill();
                process.Dispose();
            }

            File.Delete(configPath);
        }
    }

    private async Task<ServerPingResultDto> ProbeAsync(Server server, int port, CancellationToken cancellationToken)
    {
        try
        {
            var latency = await _probe.ProbeAsync(port, _url, TimeSpan.FromSeconds(_options.TimeoutSeconds), cancellationToken);
            return new ServerPingResultDto(server.Id, Math.Max(1, (int)Math.Round(latency.TotalMilliseconds)), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or IOException)
        {
            return new ServerPingResultDto(server.Id, null, Describe(ex));
        }
    }

    private async Task<bool> WaitForPortsAsync(IManagedProcess process, IReadOnlyList<int> ports, CancellationToken cancellationToken)
    {
        var deadline = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.StartTimeoutSeconds);
        var pending = ports.ToList();
        while (true)
        {
            if (process.Exited.IsCompleted)
            {
                return false;
            }

            pending = (await Task.WhenAll(pending.Select(async port => (port, open: await IsOpenAsync(port, cancellationToken)))))
                .Where(item => !item.open)
                .Select(item => item.port)
                .ToList();
            if (pending.Count == 0)
            {
                return true;
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                return false;
            }

            await Task.Delay(PortPollInterval, _timeProvider, cancellationToken);
        }
    }

    private static async Task<bool> IsOpenAsync(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private static List<int> AllocatePorts(int count)
    {
        var listeners = Enumerable.Range(0, count).Select(_ => new TcpListener(IPAddress.Loopback, 0)).ToList();
        try
        {
            listeners.ForEach(listener => listener.Start());
            return listeners.Select(listener => ((IPEndPoint)listener.LocalEndpoint).Port).ToList();
        }
        finally
        {
            listeners.ForEach(listener => listener.Stop());
        }
    }

    private static List<ServerPingResultDto> Fail(IEnumerable<Server> servers, string error) =>
        servers.Select(server => new ServerPingResultDto(server.Id, null, error)).ToList();

    private static void Remember(Queue<string> output, string line)
    {
        lock (output)
        {
            output.Enqueue(line);
            while (output.Count > OutputTailLines)
            {
                output.Dequeue();
            }
        }
    }

    private static string Tail(Queue<string> output)
    {
        lock (output)
        {
            var lines = output.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => OutboundValidator.Shorten(line.Trim())).ToList();
            return lines.Count > 0 ? lines[^1] : "порты не открылись вовремя";
        }
    }

    private static string Describe(Exception ex)
    {
        var message = ex switch
        {
            TimeoutException timeout => timeout.Message,
            HttpRequestException { InnerException: { } inner } => inner.Message,
            _ => ex.Message,
        };

        return message.Length > 200 ? message[..200] : message;
    }
}
