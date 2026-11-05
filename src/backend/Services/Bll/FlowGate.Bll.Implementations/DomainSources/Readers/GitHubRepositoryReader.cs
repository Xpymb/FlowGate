using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Чтение репозитория в формате v2fly из tarball GitHub (<c>&lt;owner&gt;-&lt;repo&gt;-&lt;sha&gt;/data/&lt;список&gt;</c>).
///     Берутся только файлы прямо в <c>data/</c>; расширение <c>.txt</c> в имени списка отбрасывается
/// </summary>
public sealed class GitHubRepositoryReader : IListSourceReader
{
    private const string DataDirectory = "data";

    private readonly long _maxBytes;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="GitHubRepositoryReader"/>
    /// </summary>
    /// <param name="options">Настройки источников (лимит распакованного размера)</param>
    /// <exception cref="ArgumentNullException">Если options равен null</exception>
    public GitHubRepositoryReader(IOptions<DomainSourceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _maxBytes = options.Value.MaxRepositoryBytes;
    }

    /// <inheritdoc />
    public DomainSourceKind Kind => DomainSourceKind.GitHubRepository;

    /// <inheritdoc />
    public async Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken)
    {
        var lists = await ReadArchiveAsync(filePath, cancellationToken);
        if (lists.Count == 0)
        {
            throw new DomainListException("В репозитории нет папки data/ со списками");
        }

        return V2flyTextLists.Parse(lists, "data/").BuildCatalog(await DatFile.Sha256Async(filePath!, cancellationToken));
    }

    /// <inheritdoc />
    public async Task<ListContent> ReadListAsync(DomainSource source, string? filePath, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        var lists = await ReadArchiveAsync(filePath, cancellationToken);
        return V2flyTextLists.Parse(lists, "data/").Resolve(name, attributeFilter);
    }

    /// <summary>
    ///     Читает тексты списков из tar.gz
    /// </summary>
    /// <param name="filePath">Путь к архиву</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Пары «имя — текст»</returns>
    /// <exception cref="DomainListException">Если архива нет, он повреждён или списки больше лимита</exception>
    public async Task<List<(string Name, string Text)>> ReadArchiveAsync(string? filePath, CancellationToken cancellationToken)
    {
        if (filePath is null || !File.Exists(filePath))
        {
            throw new DomainListException("Источник ещё не загружен: обновите его");
        }

        var lists = new List<(string Name, string Text)>();
        long total = 0;
        try
        {
            await using var file = File.OpenRead(filePath);
            await using var gzip = new GZipStream(file, CompressionMode.Decompress);
            await using var tar = new TarReader(gzip);

            while (await tar.GetNextEntryAsync(copyData: false, cancellationToken) is { } entry)
            {
                if (entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile) || entry.DataStream is null)
                {
                    continue;
                }

                var segments = entry.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length != 3 || segments[1] != DataDirectory)
                {
                    continue;
                }

                total += entry.Length;
                if (total > _maxBytes)
                {
                    throw new DomainListException($"Списки репозитория больше допустимых {_maxBytes / 1024 / 1024} МиБ");
                }

                using var reader = new StreamReader(entry.DataStream, Encoding.UTF8);
                var name = segments[2].EndsWith(".txt", StringComparison.OrdinalIgnoreCase) ? segments[2][..^4] : segments[2];
                lists.Add((name, await reader.ReadToEndAsync(cancellationToken)));
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or FormatException or IOException)
        {
            throw new DomainListException("Архив репозитория повреждён", ex);
        }

        return lists;
    }
}
