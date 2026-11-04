using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Tests.Common.Processes;

/// <summary>
///     Подменный долгоживущий процесс: работает, пока тест его не завершит или пока его не убьют
/// </summary>
public sealed class FakeManagedProcess : IManagedProcess
{
    /// <summary>
    ///     Код выхода при <see cref="Kill"/>
    /// </summary>
    public const int KilledExitCode = 137;

    private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Action<string> _onOutputLine;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FakeManagedProcess"/>
    /// </summary>
    /// <param name="id">Идентификатор процесса</param>
    /// <param name="request">Параметры запуска</param>
    /// <param name="onOutputLine">Получатель строк вывода</param>
    public FakeManagedProcess(int id, ProcessStartRequest request, Action<string> onOutputLine)
    {
        Id = id;
        Request = request;
        _onOutputLine = onOutputLine;
    }

    /// <inheritdoc />
    public int Id { get; }

    /// <summary>
    ///     Параметры, с которыми процесс запущен
    /// </summary>
    public ProcessStartRequest Request { get; }

    /// <summary>
    ///     Процесс убит через <see cref="Kill"/>
    /// </summary>
    public bool WasKilled { get; private set; }

    /// <inheritdoc />
    public Task<int> Exited => _exited.Task;

    /// <inheritdoc />
    public void Kill()
    {
        WasKilled = true;
        _exited.TrySetResult(KilledExitCode);
    }

    /// <summary>
    ///     Завершает процесс, как будто он упал сам
    /// </summary>
    /// <param name="exitCode">Код выхода</param>
    public void Exit(int exitCode) => _exited.TrySetResult(exitCode);

    /// <summary>
    ///     Печатает строку, как будто её вывел процесс
    /// </summary>
    /// <param name="line">Строка</param>
    public void WriteLine(string line) => _onOutputLine(line);

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
