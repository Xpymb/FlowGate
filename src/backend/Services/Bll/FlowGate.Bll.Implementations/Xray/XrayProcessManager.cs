using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Менеджер процессов Xray: запускает их, помнит до завершения, убивает чужие и свои при остановке FlowGate
/// </summary>
public sealed class XrayProcessManager : IXrayProcessManager, IDisposable
{
    private readonly ConcurrentDictionary<int, IManagedProcess> _processes = new();
    private readonly IProcessLauncher _launcher;
    private readonly IProcessTable _table;
    private readonly ILogger<XrayProcessManager> _logger;
    private readonly string _binaryPath;
    private readonly string _workDirectory;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayProcessManager"/>
    /// </summary>
    /// <param name="launcher">Запуск процессов</param>
    /// <param name="table">Процессы ОС</param>
    /// <param name="options">Настройки Xray: бинарник и рабочий каталог</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayProcessManager(
        IProcessLauncher launcher,
        IProcessTable table,
        IOptions<XrayOptions> options,
        ILogger<XrayProcessManager> logger)
    {
        _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        _table = table ?? throw new ArgumentNullException(nameof(table));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
        _workDirectory = Path.GetFullPath(options.Value.WorkDirectory);
    }

    /// <inheritdoc />
    public IManagedProcess Start(IReadOnlyList<string> arguments, Action<string> onOutputLine)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(onOutputLine);

        Directory.CreateDirectory(_workDirectory);
        var process = _launcher.Start(new ProcessStartRequest(_binaryPath, arguments, _workDirectory), onOutputLine);
        _processes[process.Id] = process;
        _ = ForgetOnExitAsync(process);
        return process;
    }

    /// <inheritdoc />
    public Task<int> KillStrangersAsync(CancellationToken cancellationToken)
    {
        var strangers = _table.FindByExecutable(_binaryPath).Where(pid => !_processes.ContainsKey(pid)).ToList();
        foreach (var pid in strangers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _table.Kill(pid);
            _logger.LogWarning("Остановлен посторонний процесс Xray (pid {Pid}), запущенный не этим FlowGate", pid);
        }

        return Task.FromResult(strangers.Count);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var process in _processes.Values)
        {
            process.Kill();
        }

        _processes.Clear();
    }

    private async Task ForgetOnExitAsync(IManagedProcess process)
    {
        await process.Exited;
        _processes.TryRemove(new KeyValuePair<int, IManagedProcess>(process.Id, process));
    }
}
