namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Запуск долгоживущих процессов с перехватом вывода
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    ///     Запускает процесс
    /// </summary>
    /// <param name="request">Что и как запустить</param>
    /// <param name="onOutputLine">Вызывается на каждую строку stdout и stderr</param>
    /// <returns>Запущенный процесс</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">Если файл не удалось запустить</exception>
    IManagedProcess Start(ProcessStartRequest request, Action<string> onOutputLine);
}
