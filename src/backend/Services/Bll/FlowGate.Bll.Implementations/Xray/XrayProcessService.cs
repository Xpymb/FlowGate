using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Changes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Супервизор Xray: запускает дочерний процесс, после неожиданного завершения перезапускает его
///     с паузой 1, 2, 4 … 60 с (счётчик сбрасывается, если процесс проработал минуту)
/// </summary>
public sealed class XrayProcessService : IXrayProcessService, IDisposable
{
    private static readonly TimeSpan StableUptime = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(10);
    private const int ErrorTailLines = 5;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Lock _sync = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly IXrayProcessManager _processes;
    private readonly IXrayLogBuffer _logs;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<XrayProcessService> _logger;
    private readonly string _binaryPath;
    private readonly string _configPath;
    private readonly TimeSpan _startTimeout;
    private readonly Control _control;
    private readonly IChangeFeed? _changes;

    private IManagedProcess? _process;
    private XrayProcessState _state = XrayProcessState.Stopped;
    private DateTimeOffset? _startedAt;
    private int _restarts;
    private int _consecutiveFailures;
    private int? _lastExitCode;
    private string? _lastError;
    private bool _desiredRunning;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayProcessService"/>
    /// </summary>
    /// <param name="options">Настройки Xray</param>
    /// <param name="configFile">Файл конфигурации Xray</param>
    /// <param name="processes">Менеджер процессов Xray</param>
    /// <param name="logs">Буфер вывода Xray</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <param name="changes">Шина изменений: тема <see cref="ChangeTopics.Xray"/> при смене состояния; null — не сообщать</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент, кроме changes, равен null</exception>
    public XrayProcessService(
        IOptions<XrayOptions> options,
        IXrayConfigFileProvider configFile,
        IXrayProcessManager processes,
        IXrayLogBuffer logs,
        TimeProvider timeProvider,
        ILogger<XrayProcessService> logger,
        IChangeFeed? changes = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configFile);
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
        _configPath = configFile.ConfigPath;
        _startTimeout = TimeSpan.FromSeconds(options.Value.StartTimeoutSeconds);
        _control = new Control(this);
        _changes = changes;
    }

    /// <inheritdoc />
    public XrayProcessStatus GetStatus()
    {
        lock (_sync)
        {
            var state = _state is XrayProcessState.Stopped or XrayProcessState.NotInstalled
                ? File.Exists(_binaryPath) ? XrayProcessState.Stopped : XrayProcessState.NotInstalled
                : _state;

            return new XrayProcessStatus(state, _process?.Id, _startedAt, _restarts, _lastExitCode, _lastError);
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) =>
        RunExclusiveAsync(
            async (control, token) =>
            {
                await control.StartAsync(token);
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) =>
        RunExclusiveAsync(
            async (control, token) =>
            {
                await control.StopAsync(token);
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public Task RestartAsync(CancellationToken cancellationToken) =>
        RunExclusiveAsync(
            async (control, token) =>
            {
                await control.StopAsync(token);
                await control.StartAsync(token);
                return true;
            },
            cancellationToken);

    /// <inheritdoc />
    public async Task<T> RunExclusiveAsync<T>(
        Func<IXrayProcessControl, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await action(_control, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _lifetime.Cancel();

        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        process?.Kill();
        process?.Dispose();
    }

    private bool IsRunning
    {
        get
        {
            lock (_sync)
            {
                return _process is { Exited.IsCompleted: false };
            }
        }
    }

    private async Task StartCoreAsync(CancellationToken cancellationToken)
    {
        _desiredRunning = true;
        if (IsRunning)
        {
            return;
        }

        if (!File.Exists(_binaryPath))
        {
            SetState(XrayProcessState.NotInstalled, "Xray не установлен");
            throw new XrayOperationException("Xray не установлен");
        }

        if (!File.Exists(_configPath))
        {
            SetState(XrayProcessState.Failed, "Нет файла конфигурации Xray");
            throw new XrayOperationException($"Нет файла конфигурации Xray: {_configPath}");
        }

        SetState(XrayProcessState.Starting, null);

        IManagedProcess process;
        try
        {
            process = _processes.Start(["run", "-c", _configPath], OnOutput);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or IOException)
        {
            var error = $"Не удалось запустить Xray: {ex.Message}";
            SetState(XrayProcessState.Failed, error);
            throw new XrayOperationException(error, ex);
        }

        var startupDelay = Task.Delay(_startTimeout, _timeProvider, cancellationToken);
        var finished = await Task.WhenAny(process.Exited, startupDelay);
        if (finished == process.Exited)
        {
            var exitCode = await process.Exited;
            process.Dispose();
            var error = $"Xray завершился сразу после запуска с кодом {exitCode}. {LastOutput()}".Trim();
            lock (_sync)
            {
                _lastExitCode = exitCode;
            }

            SetState(XrayProcessState.Failed, error);
            throw new XrayOperationException(error);
        }

        try
        {
            await startupDelay;
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            process.Dispose();
            SetState(XrayProcessState.Stopped, null);
            throw;
        }

        var startedAt = _timeProvider.GetUtcNow();
        lock (_sync)
        {
            _process = process;
            _startedAt = startedAt;
            _state = XrayProcessState.Running;
            _lastError = null;
        }

        NotifyChanged();

        _logger.LogInformation("Xray запущен (pid {Pid})", process.Id);
        _ = SuperviseAsync(process, startedAt);
    }

    private async Task StopCoreAsync(CancellationToken cancellationToken)
    {
        _desiredRunning = false;

        IManagedProcess? process;
        lock (_sync)
        {
            process = _process;
            _process = null;
        }

        if (process is not null)
        {
            process.Kill();
            try
            {
                await process.Exited.WaitAsync(StopTimeout, _timeProvider, cancellationToken);
            }
            catch (TimeoutException)
            {
                _logger.LogWarning("Xray (pid {Pid}) не завершился за {Seconds} с после остановки", process.Id, StopTimeout.TotalSeconds);
            }

            process.Dispose();
            _logger.LogInformation("Xray остановлен");
        }

        SetState(XrayProcessState.Stopped, null);
    }

    private async Task SuperviseAsync(IManagedProcess process, DateTimeOffset startedAt)
    {
        var exitCode = await process.Exited;

        TimeSpan delay;
        lock (_sync)
        {
            if (!ReferenceEquals(_process, process))
            {
                return;
            }

            _process = null;
            _lastExitCode = exitCode;
            _state = XrayProcessState.Restarting;
            _consecutiveFailures = _timeProvider.GetUtcNow() - startedAt >= StableUptime ? 1 : _consecutiveFailures + 1;
            delay = Backoff(_consecutiveFailures);
        }

        NotifyChanged();
        process.Dispose();
        _logger.LogWarning(
            "Xray неожиданно завершился с кодом {ExitCode}, перезапуск через {Delay} с. {Output}",
            exitCode,
            delay.TotalSeconds,
            LastOutput());

        await RestartAfterCrashAsync(delay);
    }

    private async Task RestartAfterCrashAsync(TimeSpan delay)
    {
        while (!_lifetime.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(delay, _timeProvider, _lifetime.Token);
                var restarted = await RunExclusiveAsync(
                    async (control, token) =>
                    {
                        if (!_desiredRunning || control.IsRunning)
                        {
                            return false;
                        }

                        await control.StartAsync(token);
                        return true;
                    },
                    _lifetime.Token);

                if (restarted)
                {
                    lock (_sync)
                    {
                        _restarts++;
                    }
                }

                return;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (XrayOperationException ex)
            {
                lock (_sync)
                {
                    _consecutiveFailures++;
                    delay = Backoff(_consecutiveFailures);
                    _state = XrayProcessState.Restarting;
                }

                NotifyChanged();

                _logger.LogWarning("Перезапуск Xray не удался: {Error}. Следующая попытка через {Delay} с", ex.Message, delay.TotalSeconds);
            }
        }
    }

    private static TimeSpan Backoff(int failures)
    {
        var seconds = Math.Pow(2, Math.Clamp(failures - 1, 0, 10));
        return TimeSpan.FromSeconds(Math.Min(seconds, MaxBackoff.TotalSeconds));
    }

    private void OnOutput(string line)
    {
        _logs.Add(line);
        if (line.Contains("[Error]", StringComparison.Ordinal))
        {
            _logger.LogWarning("xray: {Line}", line);
        }
    }

    private string LastOutput()
    {
        var lines = _logs.Tail(ErrorTailLines);
        return lines.Count == 0 ? string.Empty : "Последний вывод: " + string.Join(" | ", lines.Select(line => line.Text));
    }

    private void SetState(XrayProcessState state, string? error)
    {
        lock (_sync)
        {
            _state = state;
            _lastError = error;
        }

        NotifyChanged();
    }

    private void NotifyChanged() => _changes?.Publish(ChangeTopics.Xray);

    private sealed class Control : IXrayProcessControl
    {
        private readonly XrayProcessService _owner;

        public Control(XrayProcessService owner)
        {
            _owner = owner;
        }

        public bool IsRunning => _owner.IsRunning;

        public Task StartAsync(CancellationToken cancellationToken) => _owner.StartCoreAsync(cancellationToken);

        public Task StopAsync(CancellationToken cancellationToken) => _owner.StopCoreAsync(cancellationToken);
    }
}
