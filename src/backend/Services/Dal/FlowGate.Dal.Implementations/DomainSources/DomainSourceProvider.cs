using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.DomainSources;

/// <summary>
///     Источники в <c>domain-sources.json</c>
/// </summary>
public sealed class DomainSourceProvider : IDomainSourceProvider
{
    /// <summary>
    ///     Имя файла источников
    /// </summary>
    public const string FileName = "domain-sources.json";

    private readonly IJsonFileStore<DomainSourceCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainSourceProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public DomainSourceProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.DomainSourceCatalog, () => DomainSourceCatalog.Default);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainSource>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Sources;

    /// <inheritdoc />
    public async Task<DomainSource?> GetAsync(string id, CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Sources.FirstOrDefault(source => source.Id == id);

    /// <inheritdoc />
    public Task AddAsync(DomainSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        return _store.UpdateAsync(
            catalog => catalog.Sources.Any(existing => existing.Id == source.Id)
                ? throw new InvalidOperationException($"Источник {source.Id} уже существует")
                : new DomainSourceCatalog([.. catalog.Sources, source]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DomainSource?> UpdateAsync(string id, Func<DomainSource, DomainSource> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        DomainSource? updated = null;
        await _store.UpdateAsync(
            catalog => new DomainSourceCatalog(catalog.Sources
                .Select(existing => existing.Id == id ? updated = update(existing) : existing)
                .ToList()),
            cancellationToken);

        return updated;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Sources.Any(source => source.Id == id);
                return found ? new DomainSourceCatalog(catalog.Sources.Where(source => source.Id != id).ToList()) : catalog;
            },
            cancellationToken);

        return found;
    }
}
