using FlowGate.Contracts.DomainSources;

namespace FlowGate.Dal.Interfaces.DomainSources;

/// <summary>
///     Файлы источников в <c>&lt;DataDirectory&gt;/sources/</c>: загруженный <c>.dat</c> или архив репозитория и кеш
///     каталога списков
/// </summary>
public interface IDomainSourceFileProvider
{
    /// <summary>
    ///     Возвращает путь к загруженному файлу источника
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <returns>Путь или null, если файл ещё не загружен</returns>
    string? GetFilePath(string sourceId);

    /// <summary>
    ///     Возвращает путь для загрузки новой версии рядом с действующей (та же файловая система)
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <returns>Путь кандидата; каталог создан</returns>
    string CreateCandidatePath(string sourceId);

    /// <summary>
    ///     Делает кандидата действующим файлом
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <param name="candidatePath">Путь кандидата</param>
    void Commit(string sourceId, string candidatePath);

    /// <summary>
    ///     Удаляет кандидата, если он остался
    /// </summary>
    /// <param name="candidatePath">Путь кандидата</param>
    void Discard(string candidatePath);

    /// <summary>
    ///     Читает кеш каталога
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Каталог или null</returns>
    Task<SourceListCatalog?> ReadCatalogAsync(string sourceId, CancellationToken cancellationToken);

    /// <summary>
    ///     Записывает кеш каталога
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <param name="catalog">Каталог</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task WriteCatalogAsync(string sourceId, SourceListCatalog catalog, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет файл и кеш каталога источника
    /// </summary>
    /// <param name="sourceId">Источник</param>
    void Delete(string sourceId);
}
