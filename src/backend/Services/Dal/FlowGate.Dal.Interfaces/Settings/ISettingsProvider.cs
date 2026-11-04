using FlowGate.Contracts.Settings;

namespace FlowGate.Dal.Interfaces.Settings;

/// <summary>
///     Хранение пользовательских настроек (<c>settings.json</c>)
/// </summary>
public interface ISettingsProvider
{
    /// <summary>
    ///     Читает настройки; если файла нет — значения по умолчанию
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Настройки</returns>
    Task<AppSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Изменяет и сохраняет настройки под одной блокировкой
    /// </summary>
    /// <param name="update">Функция, возвращающая новые настройки по текущим</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сохранённые настройки</returns>
    Task<AppSettings> UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken);
}
