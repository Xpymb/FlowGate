using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Dal.Interfaces.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.UpdateProxy;

/// <summary>
///     Прокси обновлений — второй экземпляр Xray со своим конфигом, запускается через <see cref="IXrayProcessManager"/>.
///     Первая аренда запускает его, после последней он сразу останавливается. Упавший экземпляр не перезапускается:
///     это сделает следующая аренда
/// </summary>
public sealed class UpdateProxyService : IUpdateProxyService
{
    /// <summary>
    ///     Имя файла конфигурации в <see cref="XrayOptions.WorkDirectory"/>
    /// </summary>
    public const string ConfigFileName = "update-proxy.json";

    /// <summary>
    ///     Имя файла конфигурации экземпляра для скрипта самообновления в <see cref="XrayOptions.WorkDirectory"/>
    /// </summary>
    public const string StandaloneConfigFileName = "update-proxy-upgrade.json";

    private const string LogLevel = "warning";
    private const int OutputTailLines = 5;
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMilliseconds(200);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly Queue<string> _output = new();
    private readonly ISettingsProvider _settings;
    private readonly IServerProvider _servers;
    private readonly ISubscriptionProvider _subscriptions;
    private readonly IDomainSourceProvider _sources;
    private readonly IXrayConfigBuilder _builder;
    private readonly IProcessRunner _processRunner;
    private readonly IXrayProcessManager _processes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UpdateProxyService> _logger;
    private readonly UpdateProxyOptions _options;
    private readonly string _binaryPath;
    private readonly string _workDirectory;
    private readonly string _configPath;
    private readonly Uri _proxyUri;

    private IManagedProcess? _process;
    private byte[]? _runningConfig;
    private DateTimeOffset? _startedAt;
    private int _leases;
    private string? _reason;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyService"/>
    /// </summary>
    /// <param name="settings">Пользовательские настройки (включён ли прокси, сервер, свои домены)</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="subscriptions">Хранилище подписок (их хосты идут через прокси)</param>
    /// <param name="sources">Хранилище источников (хосты прямых ссылок идут через прокси)</param>
    /// <param name="builder">Сборщик конфигурации Xray</param>
    /// <param name="processRunner">Запуск <c>xray run -test</c></param>
    /// <param name="processes">Менеджер процессов Xray</param>
    /// <param name="xrayOptions">Настройки Xray: бинарник и рабочий каталог</param>
    /// <param name="options">Настройки прокси обновлений</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public UpdateProxyService(
        ISettingsProvider settings,
        IServerProvider servers,
        ISubscriptionProvider subscriptions,
        IDomainSourceProvider sources,
        IXrayConfigBuilder builder,
        IProcessRunner processRunner,
        IXrayProcessManager processes,
        IOptions<XrayOptions> xrayOptions,
        IOptions<UpdateProxyOptions> options,
        TimeProvider timeProvider,
        ILogger<UpdateProxyService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        ArgumentNullException.ThrowIfNull(xrayOptions);
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _binaryPath = Path.GetFullPath(xrayOptions.Value.BinaryPath);
        _workDirectory = Path.GetFullPath(xrayOptions.Value.WorkDirectory);
        _configPath = Path.Combine(_workDirectory, ConfigFileName);
        _proxyUri = new Uri($"http://{UpdateProxyXrayModel.ListenAddress}:{_options.Port}/");
    }

    /// <inheritdoc />
    public async Task<UpdateProxyLease?> AcquireAsync(UpdateVia via, CancellationToken cancellationToken)
    {
        if (via == UpdateVia.Direct)
        {
            return null;
        }

        var settings = await _settings.GetAsync(cancellationToken);
        if (via == UpdateVia.Auto && !settings.UpdateProxy.Enabled)
        {
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var config = await BuildConfigAsync(settings.UpdateProxy, _options.Port, cancellationToken);

            bool restart;
            lock (_sync)
            {
                restart = _process is null || (_leases == 0 && !_runningConfig.AsSpan().SequenceEqual(config));
            }

            if (restart)
            {
                await StopCoreAsync(cancellationToken);
                await StartCoreAsync(config, cancellationToken);
            }

            lock (_sync)
            {
                _leases++;
                _reason = null;
            }

            return new UpdateProxyLease(_proxyUri, Release);
        }
        catch (UnavailableException ex)
        {
            ReportUnavailable(ex.Message);
            if (via == UpdateVia.Proxy)
            {
                throw new ExternalServiceUnavailableException($"Прокси обновлений недоступен: {ex.Message}");
            }

            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<UpdateProxyStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        lock (_sync)
        {
            var state = _process is not null ? UpdateProxyState.Running
                : !settings.UpdateProxy.Enabled ? UpdateProxyState.Disabled
                : _reason is not null ? UpdateProxyState.Unavailable
                : UpdateProxyState.Idle;

            return new UpdateProxyStatus(state, _options.Port, _process?.Id, _startedAt, _leases, _reason);
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            bool idle;
            lock (_sync)
            {
                idle = _leases == 0;
                _reason = null;
            }

            if (idle)
            {
                await StopCoreAsync(cancellationToken);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<UpdateProxyStandaloneConfig?> PrepareStandaloneAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.UpdateProxy.Enabled)
        {
            return null;
        }

        var path = Path.Combine(_workDirectory, StandaloneConfigFileName);
        try
        {
            var config = await BuildConfigAsync(settings.UpdateProxy, _options.UpgradePort, cancellationToken);
            await WriteTestedAsync(config, path, cancellationToken);
            return new UpdateProxyStandaloneConfig(_binaryPath, path, _options.UpgradePort);
        }
        catch (UnavailableException ex)
        {
            ReportUnavailable(ex.Message);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await StopCoreAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<byte[]> BuildConfigAsync(UpdateProxySettings settings, int port, CancellationToken cancellationToken)
    {
        if (!File.Exists(_binaryPath))
        {
            throw new UnavailableException("Xray не установлен");
        }

        if (string.IsNullOrWhiteSpace(settings.ServerId))
        {
            throw new UnavailableException("сервер не выбран");
        }

        var server = await _servers.GetAsync(settings.ServerId, cancellationToken)
            ?? throw new UnavailableException($"сервер {settings.ServerId} не найден");

        var subscriptionUrls = (await _subscriptions.GetAllAsync(cancellationToken)).Select(subscription => subscription.Url);
        var sourceUrls = (await _sources.GetAllAsync(cancellationToken))
            .Select(source => source.Dat?.Url)
            .OfType<string>();

        var domains = UpdateProxyDomains.Collect(subscriptionUrls.Concat(sourceUrls), settings.ExtraDomains);
        var model = UpdateProxyXrayModel.Build(LogLevel, server.Outbound, domains, port);

        try
        {
            return Serialize(_builder.Build(model));
        }
        catch (XrayOperationException ex)
        {
            throw new UnavailableException(ex.Message);
        }
    }

    private async Task StartCoreAsync(byte[] config, CancellationToken cancellationToken)
    {
        await WriteTestedAsync(config, _configPath, cancellationToken);

        lock (_sync)
        {
            _output.Clear();
        }

        IManagedProcess process;
        try
        {
            process = _processes.Start(["run", "-c", _configPath], OnOutput);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            throw new UnavailableException($"не удалось запустить Xray: {ex.Message}");
        }

        await WaitForPortAsync(process, cancellationToken);

        lock (_sync)
        {
            _process = process;
            _runningConfig = config;
            _startedAt = _timeProvider.GetUtcNow();
        }

        _logger.LogInformation("Прокси обновлений запущен (pid {Pid}, порт {Port})", process.Id, _options.Port);
        _ = WatchAsync(process);
    }

    /// <remarks>Xray определяет формат конфига по расширению, поэтому кандидат тоже оканчивается на <c>.json</c></remarks>
    private async Task WriteTestedAsync(byte[] config, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var candidate = Path.ChangeExtension(path, ".new.json");
        try
        {
            await File.WriteAllBytesAsync(candidate, config, cancellationToken);
            await TestAsync(candidate, cancellationToken);
            File.Move(candidate, path, overwrite: true);
        }
        finally
        {
            File.Delete(candidate);
        }
    }

    private async Task TestAsync(string configPath, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(_binaryPath, ["run", "-test", "-c", configPath], TestTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            throw new UnavailableException($"не удалось проверить конфигурацию: {ex.Message}");
        }

        if (!result.Succeeded)
        {
            var errors = OutboundValidator.ExtractErrors(result.CombinedOutput, configPath);
            throw new UnavailableException($"Xray отклонил конфигурацию: {string.Join("; ", errors)}");
        }
    }

    private async Task WaitForPortAsync(IManagedProcess process, CancellationToken cancellationToken)
    {
        var deadline = _timeProvider.GetUtcNow() + TimeSpan.FromSeconds(_options.StartTimeoutSeconds);
        while (true)
        {
            if (process.Exited.IsCompleted)
            {
                var exitCode = await process.Exited;
                process.Dispose();
                throw new UnavailableException($"Xray завершился сразу после запуска с кодом {exitCode}. {OutputTail()}".Trim());
            }

            if (await CanConnectAsync(cancellationToken))
            {
                return;
            }

            if (_timeProvider.GetUtcNow() >= deadline)
            {
                process.Kill();
                process.Dispose();
                throw new UnavailableException($"Xray не начал принимать соединения за {_options.StartTimeoutSeconds} с");
            }

            try
            {
                await Task.WhenAny(process.Exited, Task.Delay(ProbeInterval, _timeProvider, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
                process.Kill();
                process.Dispose();
                throw;
            }
        }
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, _options.Port, cancellationToken);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
            _runningConfig = null;
            _startedAt = null;
        }

        if (process is null)
        {
            return;
        }

        process.Kill();
        try
        {
            await process.Exited.WaitAsync(StopTimeout, _timeProvider, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Прокси обновлений (pid {Pid}) не завершился за {Seconds} с", process.Id, StopTimeout.TotalSeconds);
        }

        process.Dispose();
        _logger.LogInformation("Прокси обновлений остановлен");
    }

    private async Task WatchAsync(IManagedProcess process)
    {
        var exitCode = await process.Exited;

        lock (_sync)
        {
            if (!ReferenceEquals(_process, process))
            {
                return;
            }

            _process = null;
            _runningConfig = null;
            _startedAt = null;
            _reason = $"Xray завершился с кодом {exitCode}";
        }

        process.Dispose();
        _logger.LogWarning("Прокси обновлений неожиданно завершился с кодом {ExitCode}. {Output}", exitCode, OutputTail());
    }

    private void Release()
    {
        lock (_sync)
        {
            _leases--;
            if (_leases > 0 || _process is null)
            {
                return;
            }
        }

        _ = StopWhenIdleAsync();
    }

    private async Task StopWhenIdleAsync()
    {
        await _gate.WaitAsync();
        try
        {
            bool idle;
            lock (_sync)
            {
                idle = _leases == 0;
            }

            if (idle)
            {
                await StopCoreAsync(CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ReportUnavailable(string reason)
    {
        bool changed;
        lock (_sync)
        {
            changed = !string.Equals(_reason, reason, StringComparison.Ordinal);
            _reason = reason;
        }

        if (changed)
        {
            _logger.LogWarning("Прокси обновлений недоступен: {Reason}", reason);
        }
    }

    private void OnOutput(string line)
    {
        lock (_sync)
        {
            _output.Enqueue(line);
            while (_output.Count > OutputTailLines)
            {
                _output.Dequeue();
            }
        }
    }

    private string OutputTail()
    {
        lock (_sync)
        {
            return _output.Count == 0 ? string.Empty : "Последний вывод: " + string.Join(" | ", _output);
        }
    }

    private static byte[] Serialize(JsonObject config)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            config.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    private sealed class UnavailableException(string message) : Exception(message);
}
