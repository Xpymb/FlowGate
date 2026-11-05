using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Tests.Common.Processes;

/// <summary>
///     Подменная таблица процессов ОС: процессы задаются тестом, завершение запоминается
/// </summary>
public sealed class FakeProcessTable : IProcessTable
{
    private readonly Lock _sync = new();
    private readonly Dictionary<int, string> _processes = [];
    private readonly List<int> _killed = [];

    /// <summary>
    ///     Убитые pid
    /// </summary>
    public IReadOnlyList<int> Killed
    {
        get
        {
            lock (_sync)
            {
                return _killed.ToList();
            }
        }
    }

    /// <summary>
    ///     Добавляет процесс
    /// </summary>
    /// <param name="pid">Идентификатор</param>
    /// <param name="executable">Путь бинарника</param>
    public void Add(int pid, string executable)
    {
        lock (_sync)
        {
            _processes[pid] = executable;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<int> FindByExecutable(string executablePrefix)
    {
        lock (_sync)
        {
            return _processes.Where(process => process.Value.StartsWith(executablePrefix, StringComparison.Ordinal)).Select(process => process.Key).ToList();
        }
    }

    /// <inheritdoc />
    public void Kill(int pid)
    {
        lock (_sync)
        {
            _processes.Remove(pid);
            _killed.Add(pid);
        }
    }
}
