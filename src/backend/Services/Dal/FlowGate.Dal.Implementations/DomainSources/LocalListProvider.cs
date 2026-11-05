using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.DomainSources;

/// <summary>
///     Списки локальных источников в <c>local-lists.json</c>
/// </summary>
public sealed class LocalListProvider : ILocalListProvider
{
    /// <summary>
    ///     Имя файла локальных списков
    /// </summary>
    public const string FileName = "local-lists.json";

    private readonly IJsonFileStore<LocalListCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="LocalListProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public LocalListProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.LocalListCatalog, () => LocalListCatalog.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalList>> GetAllAsync(string sourceId, CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Lists
            .Where(list => list.SourceId == sourceId)
            .OrderBy(list => list.Name, StringComparer.Ordinal)
            .ToList();

    /// <inheritdoc />
    public Task UpsertAsync(LocalList list, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(list);

        return _store.UpdateAsync(
            catalog => new LocalListCatalog(
            [
                .. catalog.Lists.Where(existing => !(existing.SourceId == list.SourceId && existing.Name == list.Name)),
                list,
            ]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string sourceId, string name, CancellationToken cancellationToken)
    {
        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Lists.Any(list => list.SourceId == sourceId && list.Name == name);
                return found
                    ? new LocalListCatalog(catalog.Lists.Where(list => !(list.SourceId == sourceId && list.Name == name)).ToList())
                    : catalog;
            },
            cancellationToken);

        return found;
    }

    /// <inheritdoc />
    public Task DeleteSourceAsync(string sourceId, CancellationToken cancellationToken) =>
        _store.UpdateAsync(
            catalog => new LocalListCatalog(catalog.Lists.Where(list => list.SourceId != sourceId).ToList()),
            cancellationToken);
}
