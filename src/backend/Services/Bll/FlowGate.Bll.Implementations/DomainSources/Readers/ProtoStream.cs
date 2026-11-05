using FlowGate.Contracts.DomainLists;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Потоковый перебор записей верхнего уровня protobuf-файла (<c>GeoSiteList</c>, <c>GeoIPList</c>). Для всех записей
///     используется один растущий буфер, поэтому на роутере не копится мусор в куче больших объектов
/// </summary>
public static class ProtoStream
{
    private const int InitialBufferSize = 64 * 1024;

    /// <summary>
    ///     Перебирает записи верхнего уровня (<c>repeated entry = 1</c>) списка в потоке
    /// </summary>
    /// <param name="stream">Поток файла</param>
    /// <param name="maxEntryBytes">Предельный размер одной записи</param>
    /// <returns>Байты каждой записи; действительны только до перехода к следующей</returns>
    /// <exception cref="DomainListException">Если данные повреждены</exception>
    public static IEnumerable<ReadOnlyMemory<byte>> ReadEntries(Stream stream, int maxEntryBytes)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var buffer = new byte[InitialBufferSize];
        while (TryReadStreamVarint(stream, out var tag))
        {
            if ((tag & 7) != 2)
            {
                throw ProtoReader.Corrupted();
            }

            if (!TryReadStreamVarint(stream, out var length) || length > (ulong)maxEntryBytes)
            {
                throw ProtoReader.Corrupted();
            }

            var size = (int)length;
            if (size > buffer.Length)
            {
                buffer = new byte[Math.Max(size, buffer.Length * 2)];
            }

            stream.ReadExactly(buffer, 0, size);
            if (tag >> 3 == 1)
            {
                yield return buffer.AsMemory(0, size);
            }
        }
    }

    private static bool TryReadStreamVarint(Stream stream, out ulong value)
    {
        value = 0;
        for (var shift = 0; shift < 64; shift += 7)
        {
            var current = stream.ReadByte();
            if (current < 0)
            {
                return shift == 0 ? false : throw ProtoReader.Corrupted();
            }

            value |= (ulong)(current & 0x7F) << shift;
            if ((current & 0x80) == 0)
            {
                return true;
            }
        }

        throw ProtoReader.Corrupted();
    }
}
