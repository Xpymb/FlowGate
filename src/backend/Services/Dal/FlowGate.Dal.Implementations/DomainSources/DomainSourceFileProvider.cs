using System.Text.Json;

using Microsoft.Extensions.Options;

using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Shared.Dal.Files;
using FlowGate.Shared.Options;

namespace FlowGate.Dal.Implementations.DomainSources;

/// <summary>
///     Файлы источников в <c>&lt;DataDirectory&gt;/sources/</c>: <c>&lt;id&gt;.data</c> — загруженный файл,
///     <c>&lt;id&gt;.catalog.json</c> — кеш каталога. Идентификатор источника — только <c>[a-z0-9-]</c>, поэтому путь
///     из него собирается безопасно
/// </summary>
public sealed class DomainSourceFileProvider : IDomainSourceFileProvider
{
    /// <summary>
    ///     Подкаталог файлов источников
    /// </summary>
    public const string DirectoryName = "sources";

    private readonly string _directory;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainSourceFileProvider"/>
    /// </summary>
    /// <param name="options">Настройки FlowGate (каталог данных)</param>
    /// <exception cref="ArgumentNullException">Если options равен null</exception>
    public DomainSourceFileProvider(IOptions<FlowGateOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _directory = Path.Combine(Path.GetFullPath(options.Value.DataDirectory), DirectoryName);
    }

    /// <inheritdoc />
    public string? GetFilePath(string sourceId)
    {
        var path = DataPath(sourceId);
        return File.Exists(path) ? path : null;
    }

    /// <inheritdoc />
    public string CreateCandidatePath(string sourceId)
    {
        Directory.CreateDirectory(_directory);
        return Path.Combine(_directory, $".{Guid.NewGuid():N}.{Validate(sourceId)}.new");
    }

    /// <inheritdoc />
    public void Commit(string sourceId, string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        File.Move(candidatePath, DataPath(sourceId), overwrite: true);
    }

    /// <inheritdoc />
    public void Discard(string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        AtomicFile.DeleteIfExists(candidatePath);
    }

    /// <inheritdoc />
    public async Task<SourceListCatalog?> ReadCatalogAsync(string sourceId, CancellationToken cancellationToken)
    {
        var path = CatalogPath(sourceId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync(stream, DalJsonContext.Default.SourceListCatalog, cancellationToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public Task WriteCatalogAsync(string sourceId, SourceListCatalog catalog, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        Directory.CreateDirectory(_directory);
        return AtomicFile.WriteAsync(
            CatalogPath(sourceId),
            (stream, token) => JsonSerializer.SerializeAsync(stream, catalog, DalJsonContext.Default.SourceListCatalog, token),
            cancellationToken);
    }

    /// <inheritdoc />
    public void Delete(string sourceId)
    {
        AtomicFile.DeleteIfExists(DataPath(sourceId));
        AtomicFile.DeleteIfExists(CatalogPath(sourceId));
    }

    private string DataPath(string sourceId) => Path.Combine(_directory, Validate(sourceId) + ".data");

    private string CatalogPath(string sourceId) => Path.Combine(_directory, Validate(sourceId) + ".catalog.json");

    private static string Validate(string sourceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        return sourceId.All(character => char.IsAsciiLetterLower(character) || char.IsAsciiDigit(character) || character == '-')
            ? sourceId
            : throw new ArgumentException($"Недопустимый идентификатор источника: {sourceId}", nameof(sourceId));
    }
}
