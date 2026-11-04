using System.Diagnostics;

namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Обёртка над запущенным <see cref="Process"/>
/// </summary>
public sealed class ManagedProcess : IManagedProcess
{
    private readonly Process _process;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ManagedProcess"/>
    /// </summary>
    /// <param name="process">Уже запущенный процесс с <c>EnableRaisingEvents</c></param>
    /// <exception cref="ArgumentNullException">Если process равен null</exception>
    public ManagedProcess(Process process)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        Id = process.Id;
        Exited = WaitForExitAsync(process);
    }

    /// <inheritdoc />
    public int Id { get; }

    /// <inheritdoc />
    public Task<int> Exited { get; }

    /// <inheritdoc />
    public void Kill()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose() => _process.Dispose();

    private static async Task<int> WaitForExitAsync(Process process)
    {
        await process.WaitForExitAsync();
        return process.ExitCode;
    }
}
