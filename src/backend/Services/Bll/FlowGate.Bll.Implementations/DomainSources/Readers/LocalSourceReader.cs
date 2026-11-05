using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Interfaces.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Чтение локального источника: тексты v2fly из <see cref="ILocalListProvider"/>
/// </summary>
public sealed class LocalSourceReader : IListSourceReader
{
    private readonly ILocalListProvider _lists;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="LocalSourceReader"/>
    /// </summary>
    /// <param name="lists">Хранилище локальных списков</param>
    /// <exception cref="ArgumentNullException">Если lists равен null</exception>
    public LocalSourceReader(ILocalListProvider lists)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
    }

    /// <inheritdoc />
    public DomainSourceKind Kind => DomainSourceKind.Local;

    /// <inheritdoc />
    public async Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var files = await ReadAsync(source.Id, cancellationToken);
        return V2flyTextLists.Parse(files, string.Empty).BuildCatalog(V2flyTextLists.Hash(files));
    }

    /// <inheritdoc />
    public async Task<ListContent> ReadListAsync(DomainSource source, string? filePath, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);

        var files = await ReadAsync(source.Id, cancellationToken);
        return V2flyTextLists.Parse(files, string.Empty).Resolve(name, attributeFilter);
    }

    private async Task<List<(string Name, string Text)>> ReadAsync(string sourceId, CancellationToken cancellationToken) =>
        (await _lists.GetAllAsync(sourceId, cancellationToken)).Select(list => (list.Name, list.Text)).ToList();
}
