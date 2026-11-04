using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Xray как дочерний процесс FlowGate: запуск, остановка, перезапуск после падений
/// </summary>
public interface IXrayProcessService
{
    /// <summary>
    ///     Возвращает состояние процесса
    /// </summary>
    /// <returns>Снимок состояния</returns>
    XrayProcessStatus GetStatus();

    /// <summary>
    ///     Запускает Xray; после неожиданного завершения супервизор перезапускает его с нарастающей паузой
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="XrayOperationException">Если Xray не установлен, нет конфигурации или процесс сразу завершился</exception>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Останавливает Xray; супервизор его больше не перезапускает
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task StopAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Останавливает и снова запускает Xray
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="XrayOperationException">Если запуск не удался</exception>
    Task RestartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Выполняет действие под блокировкой процесса: супервизор и другие операции ждут, пока оно не закончится.
    ///     Нужно для подмены бинарника и конфигурации
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="action">Действие; управляет процессом через переданный <see cref="IXrayProcessControl"/></param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат действия</returns>
    Task<T> RunExclusiveAsync<T>(Func<IXrayProcessControl, CancellationToken, Task<T>> action, CancellationToken cancellationToken);

    /// <summary>
    ///     Убивает Xray, оставшийся от прошлого запуска FlowGate (по pid-файлу, только если это наш бинарник)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task KillOrphanAsync(CancellationToken cancellationToken);
}
