using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Релизы Xray с GitHub
/// </summary>
public interface IXrayReleaseService
{
    /// <summary>
    ///     Результат последней успешной проверки или null, если проверок ещё не было
    /// </summary>
    XrayUpdateCheck? LastCheck { get; }

    /// <summary>
    ///     Возвращает релизы, пригодные для установки, новые первыми (включая pre-release)
    /// </summary>
    /// <param name="refresh">Не брать из кэша</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Релизы</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<IReadOnlyList<XrayRelease>> GetReleasesAsync(bool refresh, CancellationToken cancellationToken);

    /// <summary>
    ///     Находит релиз
    /// </summary>
    /// <param name="version">Версия; null — последняя стабильная</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Релиз или null, если не найден</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<XrayRelease?> FindAsync(XrayVersion? version, CancellationToken cancellationToken);

    /// <summary>
    ///     Перечитывает релизы и запоминает последнюю стабильную версию
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат проверки</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<XrayUpdateCheck> CheckForUpdatesAsync(CancellationToken cancellationToken);
}
