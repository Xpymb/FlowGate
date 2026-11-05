using System.Collections.Concurrent;

using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Interfaces.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources;

/// <summary>
///     Чтение списков через читателя нужного вида с кешем собранных списков. Ключ кеша включает хеш файла из каталога,
///     поэтому новая версия источника кеш не использует
/// </summary>
public sealed class DomainListReader : IDomainListReader
{
    private const int MaxCachedLists = 64;

    private readonly IReadOnlyDictionary<DomainSourceKind, IListSourceReader> _readers;
    private readonly IDomainSourceFileProvider _files;
    private readonly ConcurrentDictionary<(string SourceId, string Sha256, string Name, string? Filter), ListContent> _cache = new();

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainListReader"/>
    /// </summary>
    /// <param name="readers">Читатели всех видов источников</param>
    /// <param name="files">Файлы источников</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public DomainListReader(IEnumerable<IListSourceReader> readers, IDomainSourceFileProvider files)
    {
        ArgumentNullException.ThrowIfNull(readers);
        _readers = readers.ToDictionary(reader => reader.Kind);
        _files = files ?? throw new ArgumentNullException(nameof(files));
    }

    /// <inheritdoc />
    public Task<SourceListCatalog?> GetCatalogAsync(DomainSource source, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        return _files.ReadCatalogAsync(source.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ListContent> ReadAsync(DomainSource source, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var catalog = await GetCatalogAsync(source, cancellationToken)
            ?? throw new DomainListException($"Источник «{source.Name}» ещё не загружен: обновите его");

        var key = (source.Id, catalog.Sha256, name, attributeFilter);
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var content = await Reader(source).ReadListAsync(source, _files.GetFilePath(source.Id), name, attributeFilter, cancellationToken);
        if (_cache.Count >= MaxCachedLists)
        {
            _cache.Clear();
        }

        _cache[key] = content;
        return content;
    }

    /// <inheritdoc />
    public async Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var catalog = await Reader(source).BuildCatalogAsync(source, filePath, cancellationToken);
        await _files.WriteCatalogAsync(source.Id, catalog, cancellationToken);
        Invalidate(source.Id);
        return catalog;
    }

    /// <inheritdoc />
    public void Invalidate(string sourceId)
    {
        foreach (var key in _cache.Keys.Where(key => key.SourceId == sourceId))
        {
            _cache.TryRemove(key, out _);
        }
    }

    private IListSourceReader Reader(DomainSource source) =>
        _readers.TryGetValue(source.Kind, out var reader)
            ? reader
            : throw new InvalidOperationException($"Нет читателя для источников вида {source.Kind}");
}
