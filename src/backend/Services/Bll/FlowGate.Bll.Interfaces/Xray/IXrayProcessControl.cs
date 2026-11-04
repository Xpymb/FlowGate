using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Управление процессом Xray внутри <see cref="IXrayProcessService.RunExclusiveAsync{T}"/>, без повторного захвата блокировки
/// </summary>
public interface IXrayProcessControl
{
    /// <summary>
    ///     Процесс запущен и не завершился
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    ///     Запускает процесс, если он не запущен
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="XrayOperationException">Если Xray не установлен, нет конфигурации или процесс сразу завершился</exception>
    Task StartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Останавливает процесс, если он запущен
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task StopAsync(CancellationToken cancellationToken);
}
