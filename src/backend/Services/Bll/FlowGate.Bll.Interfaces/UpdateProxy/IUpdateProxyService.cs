using FlowGate.Contracts.UpdateProxy;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Прокси обновлений: HTTP-inbound в основном Xray (модель собирает <c>ConnectionXrayConfigSource</c>), весь его
///     трафик идёт через выбранный сервер. Отдельный экземпляр Xray запускается только скриптом самообновления
/// </summary>
public interface IUpdateProxyService
{
    /// <summary>
    ///     Выбирает путь для запроса или для всей операции (<see cref="UpdateProxyScope"/>)
    /// </summary>
    /// <param name="via">
    ///     <see cref="UpdateVia.Auto"/> — по настройкам, <see cref="UpdateVia.Proxy"/> — через выбранный сервер даже при
    ///     выключенном прокси, <see cref="UpdateVia.Direct"/> — без прокси
    /// </param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Адрес прокси или null — идти напрямую</returns>
    /// <exception cref="FlowGate.Exceptions.ExternalServiceUnavailableException">Если выбран <see cref="UpdateVia.Proxy"/>, а прокси недоступен</exception>
    Task<Uri?> ResolveAsync(UpdateVia via, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает состояние
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Снимок состояния</returns>
    Task<UpdateProxyStatus> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Готовит проверенную конфигурацию для экземпляра, который запускает скрипт самообновления (служба и основной
    ///     Xray в это время остановлены; экземпляр слушает свой порт)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Параметры запуска или null, если прокси выключен или недоступен</returns>
    Task<UpdateProxyStandaloneConfig?> PrepareStandaloneAsync(CancellationToken cancellationToken);
}
