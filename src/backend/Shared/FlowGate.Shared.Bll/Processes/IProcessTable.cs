namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Процессы ОС: какие запущены и из какого файла, принудительное завершение
/// </summary>
public interface IProcessTable
{
    /// <summary>
    ///     Возвращает pid процессов, запущенных из файлов, путь которых начинается с <paramref name="executablePrefix"/>
    ///     (так находятся и подменённые бинарники: <c>xray.new</c>, <c>xray (deleted)</c>)
    /// </summary>
    /// <param name="executablePrefix">Полный путь бинарника</param>
    /// <returns>Идентификаторы процессов; на ОС без <c>/proc</c> — пусто</returns>
    IReadOnlyList<int> FindByExecutable(string executablePrefix);

    /// <summary>
    ///     Принудительно завершает процесс; если его уже нет, ничего не делает
    /// </summary>
    /// <param name="pid">Идентификатор процесса</param>
    void Kill(int pid);
}
