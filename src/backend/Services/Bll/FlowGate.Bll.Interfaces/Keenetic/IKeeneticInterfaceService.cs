using FlowGate.Contracts.Keenetic;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Keenetic;

/// <summary>
///     Интерфейсы Keenetic: чтение состояния и изменение через очередь операций NDMS
/// </summary>
public interface IKeeneticInterfaceService
{
    /// <summary>
    ///     Возвращает все интерфейсы роутера
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояния по имени интерфейса (без учёта регистра)</returns>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен</exception>
    Task<IReadOnlyDictionary<string, KeeneticInterfaceState>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Выполняет команды одним пакетом в очереди NDMS и сохраняет конфигурацию
    /// </summary>
    /// <param name="operationName">Название операции для логов</param>
    /// <param name="commands">Команды; пустой список — ничего не делать</param>
    /// <param name="ignoreMissing">Не считать ошибкой <c>argument parse error</c> (удаление несуществующего интерфейса)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="KeeneticCommandException">Если Keenetic отклонил команду; конфигурация при этом не сохраняется</exception>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен</exception>
    Task ExecuteAsync(string operationName, IReadOnlyList<string> commands, bool ignoreMissing, CancellationToken cancellationToken);
}
