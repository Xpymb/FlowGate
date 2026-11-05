using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.Settings;

/// <summary>
///     Настройки FlowGate, которые меняются из интерфейса
/// </summary>
public interface ISettingsService
{
    /// <summary>
    ///     Возвращает настройки с действующими значениями интервалов
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Настройки</returns>
    Task<SettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Проверяет и сохраняет настройки целиком. Смена уровня логирования пересобирает конфиг Xray, смена прокси
    ///     сбрасывает его экземпляр
    /// </summary>
    /// <param name="request">Новые настройки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сохранённые настройки</returns>
    /// <exception cref="FlowGate.Exceptions.RequestValidationException">Если значения некорректны или сервер не найден</exception>
    Task<SettingsDto> UpdateAsync(SettingsSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает состояние прокси обновлений
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Состояние</returns>
    Task<UpdateProxyStatusDto> GetUpdateProxyStatusAsync(CancellationToken cancellationToken);
}
