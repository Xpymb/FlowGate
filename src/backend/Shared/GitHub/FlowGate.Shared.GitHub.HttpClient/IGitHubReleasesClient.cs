using FlowGate.Exceptions;
using FlowGate.Shared.GitHub.Models;

namespace FlowGate.Shared.GitHub;

/// <summary>
///     Клиент релизов GitHub
/// </summary>
public interface IGitHubReleasesClient
{
    /// <summary>
    ///     Читает последние релизы репозитория, новые первыми
    /// </summary>
    /// <param name="repository">Репозиторий в формате <c>owner/repo</c></param>
    /// <param name="count">Сколько релизов вернуть (1–100)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Релизы, включая черновики и pre-release</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен, ответил ошибкой или исчерпан лимит API</exception>
    Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(string repository, int count, CancellationToken cancellationToken);

    /// <summary>
    ///     Скачивает файл потоково; файл назначения перезаписывается
    /// </summary>
    /// <param name="url">Адрес файла</param>
    /// <param name="destinationPath">Куда сохранить</param>
    /// <param name="maxBytes">Предельный размер; больше — ошибка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Размер скачанного файла, байты</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если скачивание не удалось или файл больше предела</exception>
    Task<long> DownloadAsync(Uri url, string destinationPath, long maxBytes, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает sha последнего коммита ветки
    /// </summary>
    /// <param name="repository">Репозиторий <c>owner/repo</c></param>
    /// <param name="reference">Ветка, тег или <c>HEAD</c> — ветка по умолчанию</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Sha коммита (40 hex-символов)</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен, репозитория или ветки нет, исчерпан лимит API</exception>
    Task<string> GetCommitShaAsync(string repository, string reference, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает адрес tarball репозитория на коммите для <see cref="DownloadAsync"/> (от базового адреса API)
    /// </summary>
    /// <param name="repository">Репозиторий <c>owner/repo</c></param>
    /// <param name="sha">Коммит</param>
    /// <returns>Абсолютный адрес</returns>
    Uri GetTarballUrl(string repository, string sha);
}
