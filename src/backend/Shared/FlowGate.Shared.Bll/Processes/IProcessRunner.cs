namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Запуск коротких внешних команд с ожиданием завершения
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    ///     Запускает команду и ждёт её завершения
    /// </summary>
    /// <param name="fileName">Исполняемый файл</param>
    /// <param name="arguments">Аргументы</param>
    /// <param name="timeout">Предельное время; по истечении процесс убивается</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Код выхода и вывод</returns>
    /// <exception cref="TimeoutException">Если команда не завершилась за <paramref name="timeout"/></exception>
    /// <exception cref="System.ComponentModel.Win32Exception">Если файл не удалось запустить</exception>
    Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}
