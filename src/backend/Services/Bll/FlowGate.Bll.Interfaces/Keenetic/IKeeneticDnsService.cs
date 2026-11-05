using FlowGate.Contracts.DnsRoutes;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Keenetic;

/// <summary>
///     Группы <c>object-group fqdn</c> и правила <c>dns-proxy route</c> Keenetic
/// </summary>
public interface IKeeneticDnsService
{
    /// <summary>
    ///     Читает все группы и правила
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние Keenetic</returns>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен</exception>
    Task<NdmsDnsState> GetStateAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Выполняет команды пакетами в одной операции очереди NDMS и один раз сохраняет конфигурацию
    /// </summary>
    /// <param name="operationName">Название операции для логов</param>
    /// <param name="commands">Команды по порядку; пусто — ничего не делать</param>
    /// <param name="report">Прогресс для UI или null</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="KeeneticCommandException">Если Keenetic отклонил команды; выполненные до ошибки пакеты остаются в running-config</exception>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен</exception>
    Task ExecuteAsync(string operationName, IReadOnlyList<string> commands, Action<string>? report, CancellationToken cancellationToken);
}
