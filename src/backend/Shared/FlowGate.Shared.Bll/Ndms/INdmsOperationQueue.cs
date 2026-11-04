using FlowGate.Exceptions;

namespace FlowGate.Shared.Bll.Ndms;

/// <summary>
///     Очередь операций, изменяющих конфигурацию Keenetic. Операции выполняются строго по одной:
///     параллельные записи в NDMS дают гонки
/// </summary>
public interface INdmsOperationQueue
{
    /// <summary>
    ///     Выполняет операцию, дождавшись завершения предыдущих
    /// </summary>
    /// <typeparam name="T">Тип результата</typeparam>
    /// <param name="operationName">Название операции для логов, например «Применение DNS-маршрутов»</param>
    /// <param name="operation">Операция; получает токен, который отменяется по таймауту</param>
    /// <param name="cancellationToken">Токен отмены вызывающей стороны</param>
    /// <returns>Результат операции</returns>
    /// <exception cref="KeeneticUnavailableException">Если операция не уложилась в таймаут вместе с ожиданием очереди</exception>
    Task<T> EnqueueAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Выполняет операцию без результата, дождавшись завершения предыдущих
    /// </summary>
    /// <param name="operationName">Название операции для логов</param>
    /// <param name="operation">Операция; получает токен, который отменяется по таймауту</param>
    /// <param name="cancellationToken">Токен отмены вызывающей стороны</param>
    /// <exception cref="KeeneticUnavailableException">Если операция не уложилась в таймаут вместе с ожиданием очереди</exception>
    Task EnqueueAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken);
}
