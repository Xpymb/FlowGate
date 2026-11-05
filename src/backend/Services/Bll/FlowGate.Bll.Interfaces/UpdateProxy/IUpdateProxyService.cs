using FlowGate.Contracts.UpdateProxy;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Прокси обновлений: отдельный экземпляр Xray, который запускается первой арендой и останавливается сразу после
///     последней. Основной Xray не затрагивается
/// </summary>
public interface IUpdateProxyService
{
    /// <summary>
    ///     Берёт прокси на запрос или на всю операцию (<see cref="UpdateProxyScope"/>): при необходимости запускает
    ///     экземпляр. Пока аренда не отпущена, экземпляр не останавливается и не перезапускается
    /// </summary>
    /// <param name="via">
    ///     <see cref="UpdateVia.Auto"/> — по настройкам, <see cref="UpdateVia.Proxy"/> — через выбранный сервер даже при
    ///     выключенном прокси, <see cref="UpdateVia.Direct"/> — без прокси
    /// </param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Аренда с адресом прокси или null — идти напрямую</returns>
    /// <exception cref="FlowGate.Exceptions.ExternalServiceUnavailableException">Если выбран <see cref="UpdateVia.Proxy"/>, а прокси недоступен</exception>
    Task<UpdateProxyLease?> AcquireAsync(UpdateVia via, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает состояние
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Снимок состояния</returns>
    Task<UpdateProxyStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Сбрасывает экземпляр после смены настроек: если запросов нет, останавливает его сразу; иначе новый конфиг
    ///     применится, когда запросы закончатся
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task InvalidateAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Готовит проверенную конфигурацию для экземпляра, который запускает скрипт самообновления (он переживает
    ///     остановку службы и слушает свой порт)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Параметры запуска или null, если прокси выключен или недоступен</returns>
    Task<UpdateProxyStandaloneConfig?> PrepareStandaloneAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Останавливает экземпляр при остановке FlowGate
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ShutdownAsync(CancellationToken cancellationToken);
}
