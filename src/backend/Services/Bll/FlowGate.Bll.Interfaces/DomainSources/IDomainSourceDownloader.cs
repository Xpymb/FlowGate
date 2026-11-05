using FlowGate.Contracts.DomainSources;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.DomainSources;

/// <summary>
///     Загрузка файла источника, только если он изменился
/// </summary>
public interface IDomainSourceDownloader
{
    /// <summary>
    ///     Проверяет версию источника и при изменении скачивает файл в кандидата
    /// </summary>
    /// <param name="source">Источник с файлом (не локальный)</param>
    /// <param name="hasCurrentFile">Есть ли уже загруженный файл: без него «не изменился» невозможно</param>
    /// <param name="candidatePath">Куда скачать новую версию</param>
    /// <param name="report">Прогресс для UI</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Новая версия или null, если источник не изменился и кандидат не записан</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если загрузка не удалась или контрольная сумма не совпала</exception>
    Task<DomainSourceRevision?> DownloadAsync(
        DomainSource source,
        bool hasCurrentFile,
        string candidatePath,
        Action<string> report,
        CancellationToken cancellationToken);
}
