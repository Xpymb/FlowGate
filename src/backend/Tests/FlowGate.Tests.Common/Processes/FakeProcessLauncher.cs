using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Tests.Common.Processes;

/// <summary>
///     Подменный запуск долгоживущих процессов
/// </summary>
public sealed class FakeProcessLauncher : IProcessLauncher
{
    private readonly List<FakeManagedProcess> _started = [];
    private readonly Lock _sync = new();
    private int _nextId = 1000;

    /// <summary>
    ///     Код, с которым процесс завершится сразу после запуска, или null — процесс работает
    /// </summary>
    public Func<ProcessStartRequest, int?> ExitOnStart { get; set; } = _ => null;

    /// <summary>
    ///     Запущенные процессы в порядке запуска
    /// </summary>
    public IReadOnlyList<FakeManagedProcess> Started
    {
        get
        {
            lock (_sync)
            {
                return _started.ToList();
            }
        }
    }

    /// <inheritdoc />
    public IManagedProcess Start(ProcessStartRequest request, Action<string> onOutputLine)
    {
        FakeManagedProcess process;
        lock (_sync)
        {
            process = new FakeManagedProcess(_nextId++, request, onOutputLine);
            _started.Add(process);
        }

        if (ExitOnStart(request) is { } exitCode)
        {
            process.Exit(exitCode);
        }

        return process;
    }
}
