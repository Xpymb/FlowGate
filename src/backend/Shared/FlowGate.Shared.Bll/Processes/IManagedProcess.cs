namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Запущенный долгоживущий процесс
/// </summary>
public interface IManagedProcess : IDisposable
{
    /// <summary>
    ///     Идентификатор процесса в ОС
    /// </summary>
    int Id { get; }

    /// <summary>
    ///     Завершается с кодом выхода, когда процесс завершился
    /// </summary>
    Task<int> Exited { get; }

    /// <summary>
    ///     Принудительно завершает процесс; если он уже завершён, ничего не делает
    /// </summary>
    void Kill();
}
