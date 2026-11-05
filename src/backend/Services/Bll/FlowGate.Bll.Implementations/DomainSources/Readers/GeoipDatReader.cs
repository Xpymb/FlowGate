using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Чтение <c>geoip.dat</c>. Формат Xray <c>common/geodata/geodat.proto</c>: <c>GeoIPList{entry=1}</c>,
///     <c>GeoIP{code=1, cidr=2{ip=1 bytes, prefix=2}, reverse_match=3}</c>. Атрибутов у geoip нет
/// </summary>
public sealed class GeoipDatReader : IListSourceReader
{
    /// <inheritdoc />
    public DomainSourceKind Kind => DomainSourceKind.GeoipDat;

    /// <inheritdoc />
    public async Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken)
    {
        var lists = new List<CatalogEntry>();
        DatFile.ForEachEntry(
            filePath,
            entry =>
            {
                if (ParseEntry(entry.Span, onlyCode: null) is { } parsed)
                {
                    lists.Add(CatalogEntry.From(parsed.Name, parsed.Content, []));
                }

                return true;
            },
            cancellationToken);

        return new SourceListCatalog(await DatFile.Sha256Async(filePath!, cancellationToken), GeositeDatReader.Sorted(lists), []);
    }

    /// <inheritdoc />
    public Task<ListContent> ReadListAsync(DomainSource source, string? filePath, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (attributeFilter is not null)
        {
            throw new DomainListException("У списков geoip нет атрибутов: фильтр неприменим");
        }

        ListContent? found = null;
        DatFile.ForEachEntry(
            filePath,
            entry =>
            {
                found = ParseEntry(entry.Span, name)?.Content;
                return found is null;
            },
            cancellationToken);

        return Task.FromResult(found ?? throw new DomainListException($"Списка «{name}» нет в источнике"));
    }

    private static (string Name, ListContent Content)? ParseEntry(ReadOnlySpan<byte> entry, string? onlyCode)
    {
        var reader = new ProtoReader(entry);
        string? code = null;
        var builder = new ListContentBuilder();

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
                AddCidr(reader.ReadBytes(), builder);
            }
            else
            {
                reader.Skip();
            }
        }

        return code is null ? null : (code, builder.Build());
    }

    private static void AddCidr(ReadOnlySpan<byte> message, ListContentBuilder builder)
    {
        var reader = new ProtoReader(message);
        ReadOnlySpan<byte> address = default;
        var prefix = -1;

        while (reader.Next())
        {
            if (reader.Field == 1 && reader.WireType == 2)
            {
                address = reader.ReadBytes();
            }
            else if (reader.Field == 2 && reader.WireType == 0)
            {
                prefix = (int)reader.ReadVarint();
            }
            else
            {
                reader.Skip();
            }
        }

        if (prefix < 0)
        {
            prefix = address.Length * 8;
        }

        if (ListEntries.TryFromNetwork(address, prefix, out var entry))
        {
            builder.AddNormalized(entry);
        }
        else
        {
            builder.SkipInvalid();
        }
    }
}
