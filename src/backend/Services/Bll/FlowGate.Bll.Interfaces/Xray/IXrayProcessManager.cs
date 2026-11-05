using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Единая точка запуска долгоживущих процессов Xray (основной, прокси обновлений): знает pid всех своих процессов и
///     убирает чужие, запущенные из нашего бинарника
/// </summary>
public interface IXrayProcessManager
{
    /// <summary>
    ///     Запускает установленный бинарник Xray в рабочем каталоге и запоминает процесс до его завершения
    /// </summary>
    /// <param name="arguments">Аргументы, например <c>run -c xray.json</c></param>
    /// <param name="onOutputLine">Вызывается на каждую строку stdout и stderr</param>
    /// <returns>Запущенный процесс</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">Если бинарник не удалось запустить</exception>
    IManagedProcess Start(IReadOnlyList<string> arguments, Action<string> onOutputLine);

    /// <summary>
    ///     Убивает процессы нашего бинарника Xray, которых менеджер не запускал: оставшиеся от прошлого запуска FlowGate
    ///     или от скрипта самообновления. Вызывается при старте и в <c>flowgate cleanup</c>, а не периодически:
    ///     короткие команды (<c>xray run -test</c>, <c>version</c>, <c>wg</c>) идут мимо менеджера
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько процессов убито</returns>
    Task<int> KillStrangersAsync(CancellationToken cancellationToken);
}
