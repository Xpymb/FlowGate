using FlowGate.Contracts.Maintenance;
using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Maintenance;

namespace FlowGate.Bll.Interfaces.Maintenance;

/// <summary>
///     Состояние FlowGate, проверка новых версий и самообновление через opkg
/// </summary>
public interface ISystemService
{
    /// <summary>
    ///     Возвращает версию, данные последней проверки обновлений, время работы и память FlowGate и xray
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние</returns>
    Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Читает релизы FlowGate с GitHub и запоминает последнюю стабильную версию
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат проверки</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<AppUpdateCheck> CheckForUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Проверяет релизы FlowGate, если с последней проверки прошёл интервал из настроек
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если проверка выполнялась</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<bool> CheckForUpdatesIfDueAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает задачу <c>system-upgrade</c>: скрипт пакета отвязанным процессом выполняет
    ///     <c>opkg update &amp;&amp; opkg upgrade flowgate</c>, opkg перезапускает службу. Итог виден в
    ///     <see cref="SystemStatusDto.LastUpgradeResult"/> после перезапуска
    /// </summary>
    /// <returns>Запущенная задача</returns>
    /// <exception cref="ConflictException">Если FlowGate установлен не пакетом или обновление уже запускается</exception>
    JobDto RequestUpgrade();
}
