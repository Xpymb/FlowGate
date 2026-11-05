using FlowGate.Contracts.DomainSources;

namespace FlowGate.Dal.Interfaces.DomainSources;

/// <summary>
///     Хранение источников списков (<c>domain-sources.json</c>). При первом запуске содержит встроенные источники
/// </summary>
public interface IDomainSourceProvider
{
    /// <summary>
    ///     Возвращает все источники
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Источники в порядке добавления</returns>
    Task<IReadOnlyList<DomainSource>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает источник
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Источник или null</returns>
    Task<DomainSource?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет источник
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="InvalidOperationException">Если идентификатор уже занят</exception>
    Task AddAsync(DomainSource source, CancellationToken cancellationToken);

    /// <summary>
    ///     Атомарно изменяет источник
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="update">Изменение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённый источник или null, если его нет</returns>
    Task<DomainSource?> UpdateAsync(string id, Func<DomainSource, DomainSource> update, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет источник
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если источник был</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
