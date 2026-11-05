using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Чтение <c>geosite.dat</c> (v2fly <c>dlc.dat</c> и совместимые). Формат Xray <c>common/geodata/geodat.proto</c>:
///     <c>GeoSiteList{entry=1}</c>, <c>GeoSite{code=1, domain=2}</c>,
///     <c>Domain{type=1 Substr|Regex|Domain|Full, value=2, attribute=3{key=1}}</c>. include в файле уже раскрыты
/// </summary>
public sealed class GeositeDatReader : IListSourceReader
{
    private const int TypeSubstring = 0;
    private const int TypeRegex = 1;

    /// <inheritdoc />
    public DomainSourceKind Kind => DomainSourceKind.GeositeDat;

    /// <inheritdoc />
    public async Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken)
    {
        var lists = new List<CatalogEntry>();
        DatFile.ForEachEntry(
            filePath,
            entry =>
            {
                var site = ParseSite(entry.Span, filter: null, onlyCode: null);
                if (site is not null)
                {
                    lists.Add(CatalogEntry.From(site.Value.Name, site.Value.Content, site.Value.Attributes));
                }

                return true;
            },
            cancellationToken);

        return new SourceListCatalog(await DatFile.Sha256Async(filePath!, cancellationToken), Sorted(lists), []);
    }

    /// <inheritdoc />
    public Task<ListContent> ReadListAsync(DomainSource source, string? filePath, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ListContent? found = null;
        DatFile.ForEachEntry(
            filePath,
            entry =>
            {
                found = ParseSite(entry.Span, attributeFilter, name)?.Content;
                return found is null;
            },
            cancellationToken);

        return Task.FromResult(found ?? throw new DomainListException($"Списка «{name}» нет в источнике"));
    }

    /// <summary>
    ///     Сортирует каталог по имени
    /// </summary>
    /// <param name="lists">Списки</param>
    /// <returns>Отсортированные списки</returns>
    public static IReadOnlyList<CatalogEntry> Sorted(IEnumerable<CatalogEntry> lists) =>
        lists.OrderBy(list => list.Name, StringComparer.Ordinal).ToList();

    private static (string Name, ListContent Content, IReadOnlyList<string> Attributes)? ParseSite(ReadOnlySpan<byte> entry, string? filter, string? onlyCode)
    {
        var reader = new ProtoReader(entry);
        string? code = null;
        var builder = new ListContentBuilder();
        var attributes = new SortedSet<string>(StringComparer.Ordinal);

        while (reader.Next())
        {
            if (reader.Field == 1 && reader.WireType == 2)
            {
                code = reader.ReadString().ToLowerInvariant();
                if (onlyCode is not null && !string.Equals(code, onlyCode, StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }
            else if (reader.Field == 2 && reader.WireType == 2)
            {
                AddDomain(reader.ReadBytes(), filter, builder, attributes);
            }
            else
            {
                reader.Skip();
            }
        }

        return code is null ? null : (code, builder.Build(), attributes.ToList());
    }

    private static void AddDomain(ReadOnlySpan<byte> message, string? filter, ListContentBuilder builder, SortedSet<string> catalogAttributes)
    {
        var reader = new ProtoReader(message);
        var type = 2;
        string value = string.Empty;
        var attributes = new List<string>();

        while (reader.Next())
        {
            switch (reader.Field)
            {
                case 1 when reader.WireType == 0:
                {
                    type = (int)reader.ReadVarint();
                    break;
                }
                case 2 when reader.WireType == 2:
                {
                    value = reader.ReadString();
                    break;
                }
                case 3 when reader.WireType == 2:
                {
                    attributes.Add(ReadAttributeKey(reader.ReadBytes()));
                    break;
                }
                default:
                {
                    reader.Skip();
                    break;
                }
            }
        }

        catalogAttributes.UnionWith(attributes);
        if (!V2flyRule.MatchesAttributes(attributes, filter))
        {
            return;
        }

        switch (type)
        {
            case TypeSubstring:
            {
                builder.SkipKeyword();
                break;
            }
            case TypeRegex:
            {
                builder.SkipRegexp();
                break;
            }
            default:
            {
                builder.Add(value);
                break;
            }
        }
    }

    private static string ReadAttributeKey(ReadOnlySpan<byte> message)
    {
        var reader = new ProtoReader(message);
        while (reader.Next())
        {
            if (reader.Field == 1 && reader.WireType == 2)
            {
                return reader.ReadString().ToLowerInvariant();
            }

            reader.Skip();
        }

        return string.Empty;
    }
}
