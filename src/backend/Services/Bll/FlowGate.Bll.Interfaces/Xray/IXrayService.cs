using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Xray;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Управление Xray для API: статус, релизы, установка, запуск и логи
/// </summary>
public interface IXrayService
{
    /// <summary>
    ///     Возвращает состояние Xray. В сеть не ходит: сведения об обновлении — из последней проверки
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние</returns>
    Task<XrayStatusDto> GetStatusAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает релизы, доступные для установки, новые первыми
    /// </summary>
    /// <param name="includePrerelease">Показывать pre-release</param>
    /// <param name="refresh">Не брать из кэша, перечитать с GitHub</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Релизы</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<IReadOnlyList<XrayReleaseDto>> GetReleasesAsync(bool includePrerelease, bool refresh, CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает фоновую установку версии
    /// </summary>
    /// <param name="request">Версия; null — последняя стабильная</param>
    /// <param name="via">Как загружать: по настройкам, только через прокси обновлений или напрямую</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Запущенная задача</returns>
    /// <exception cref="RequestValidationException">Если версия указана в неверном формате</exception>
    /// <exception cref="EntityNotFoundException">Если такой версии нет среди релизов</exception>
    /// <exception cref="ConflictException">Если установка уже идёт</exception>
    Task<JobDto> InstallAsync(XrayInstallRequest request, UpdateVia via, CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает Xray и включает автозапуск
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние после запуска</returns>
    /// <exception cref="XrayOperationException">Если Xray не установлен или не запустился</exception>
    Task<XrayStatusDto> StartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Останавливает Xray и выключает автозапуск
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние после остановки</returns>
    Task<XrayStatusDto> StopAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Перезапускает Xray с пересобранной конфигурацией
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние после перезапуска</returns>
    /// <exception cref="XrayOperationException">Если Xray не установлен, конфигурация отклонена или запуск не удался</exception>
    Task<XrayStatusDto> RestartAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает последние строки вывода Xray
    /// </summary>
    /// <param name="tail">Сколько строк вернуть</param>
    /// <returns>Строки, старые первыми</returns>
    XrayLogDto GetLogs(int tail);

    /// <summary>
    ///     Действия при старте FlowGate: убрать осиротевший процесс, пересобрать конфигурацию, запустить Xray при автозапуске.
    ///     Ошибки пишутся в лог, наружу не бросаются
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task InitializeAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Действия при остановке FlowGate: остановить Xray, не меняя автозапуск
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ShutdownAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Проверяет новые релизы и пишет в лог, если доступна версия новее установленной
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task CheckForUpdatesAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Проверяет новые релизы, если с последней проверки прошёл интервал из настроек
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если проверка выполнялась</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<bool> CheckForUpdatesIfDueAsync(CancellationToken cancellationToken);
}
