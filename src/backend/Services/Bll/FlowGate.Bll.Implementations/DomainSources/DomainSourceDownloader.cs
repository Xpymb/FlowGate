using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;

using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.DomainSources.Readers;
using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Contracts.DomainSources;
using FlowGate.Exceptions;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.DomainSources;

/// <summary>
///     Загрузка источников: репозиторий — tarball коммита, если sha изменился; релиз — ассет, если изменился тег, с
///     проверкой контрольной суммы; прямая ссылка — условный запрос (<c>If-None-Match</c>/<c>If-Modified-Since</c>), без
///     них — сравнение SHA-256. Прямая ссылка может содержать токен, поэтому в ошибках только хост
/// </summary>
public sealed class DomainSourceDownloader : IDomainSourceDownloader
{
    /// <summary>
    ///     Имя HTTP-клиента загрузок по прямой ссылке
    /// </summary>
    public const string HttpClientName = "domain-sources";

    private const int ChecksumMaxBytes = 64 * 1024;
    private const int CopyBufferSize = 81920;

    private readonly IGitHubReleasesClient _gitHub;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly DomainSourceOptions _options;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainSourceDownloader"/>
    /// </summary>
    /// <param name="gitHub">Клиент GitHub</param>
    /// <param name="httpClientFactory">Фабрика HTTP-клиентов (прямые ссылки)</param>
    /// <param name="options">Настройки источников</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public DomainSourceDownloader(IGitHubReleasesClient gitHub, IHttpClientFactory httpClientFactory, IOptions<DomainSourceOptions> options)
    {
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public Task<DomainSourceRevision?> DownloadAsync(
        DomainSource source,
        bool hasCurrentFile,
        string candidatePath,
        Action<string> report,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        ArgumentNullException.ThrowIfNull(report);

        var previous = hasCurrentFile ? source.Downloaded : null;
        return source.Kind switch
        {
            DomainSourceKind.GitHubRepository => DownloadRepositoryAsync(source, previous, candidatePath, report, cancellationToken),
            DomainSourceKind.GeositeDat or DomainSourceKind.GeoipDat when source.Dat is { IsDirectUrl: true } dat =>
                DownloadUrlAsync(source, dat, previous, candidatePath, report, cancellationToken),
            DomainSourceKind.GeositeDat or DomainSourceKind.GeoipDat when source.Dat is { } dat =>
                DownloadReleaseAsync(dat, previous, candidatePath, report, cancellationToken),
            _ => throw new InvalidOperationException($"Источник {source.Id} вида {source.Kind} не скачивается"),
        };
    }

    private async Task<DomainSourceRevision?> DownloadRepositoryAsync(
        DomainSource source,
        DomainSourceRevision? previous,
        string candidatePath,
        Action<string> report,
        CancellationToken cancellationToken)
    {
        var repository = source.Repository!;
        report($"Проверка последнего коммита {repository}");
        var sha = await _gitHub.GetCommitShaAsync(repository, source.Branch ?? "HEAD", cancellationToken);
        if (previous?.Revision == sha)
        {
            return null;
        }

        report($"Загрузка {repository}@{sha[..7]}");
        await _gitHub.DownloadAsync(_gitHub.GetTarballUrl(repository, sha), candidatePath, _options.MaxDownloadBytes, cancellationToken);
        return new DomainSourceRevision(sha, null, null, await DatFile.Sha256Async(candidatePath, cancellationToken));
    }

    private async Task<DomainSourceRevision?> DownloadReleaseAsync(
        DatOrigin dat,
        DomainSourceRevision? previous,
        string candidatePath,
        Action<string> report,
        CancellationToken cancellationToken)
    {
        var repository = dat.ReleaseRepository!;
        report($"Проверка последнего релиза {repository}");
        var releases = await _gitHub.GetReleasesAsync(repository, 10, cancellationToken);
        var release = releases.FirstOrDefault(candidate => !candidate.Draft
                && !candidate.Prerelease
                && candidate.Assets.Any(asset => asset.Name == dat.Asset))
            ?? throw new ExternalServiceUnavailableException($"В релизах {repository} нет файла {dat.Asset}");

        if (previous?.Revision == release.TagName)
        {
            return null;
        }

        var asset = release.Assets.First(candidate => candidate.Name == dat.Asset);
        report($"Загрузка {dat.Asset} из {repository} {release.TagName}");
        await _gitHub.DownloadAsync(new Uri(asset.BrowserDownloadUrl), candidatePath, _options.MaxDownloadBytes, cancellationToken);
        var sha256 = await DatFile.Sha256Async(candidatePath, cancellationToken);

        if (dat.ChecksumAsset is { } checksumName && release.Assets.FirstOrDefault(candidate => candidate.Name == checksumName) is { } checksum)
        {
            var checksumPath = candidatePath + ".sum";
            try
            {
                await _gitHub.DownloadAsync(new Uri(checksum.BrowserDownloadUrl), checksumPath, ChecksumMaxBytes, cancellationToken);
                EnsureChecksum(await File.ReadAllTextAsync(checksumPath, cancellationToken), sha256, checksumName);
            }
            finally
            {
                File.Delete(checksumPath);
            }
        }

        return new DomainSourceRevision(release.TagName, null, null, sha256);
    }

    private async Task<DomainSourceRevision?> DownloadUrlAsync(
        DomainSource source,
        DatOrigin dat,
        DomainSourceRevision? previous,
        string candidatePath,
        Action<string> report,
        CancellationToken cancellationToken)
    {
        var host = source.SafeOrigin;
        var client = _httpClientFactory.CreateClient(HttpClientName);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.DownloadTimeoutSeconds));

        report($"Загрузка с {host}");
        using var request = new HttpRequestMessage(HttpMethod.Get, dat.Url);
        if (previous?.ETag is { } etag && EntityTagHeaderValue.TryParse(etag, out var tag))
        {
            request.Headers.IfNoneMatch.Add(tag);
        }

        if (previous?.LastModified is { } lastModified && DateTimeOffset.TryParse(lastModified, out var modified))
        {
            request.Headers.IfModifiedSince = modified;
        }

        try
        {
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotModified && previous is not null)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ExternalServiceUnavailableException($"{host} ответил HTTP {(int)response.StatusCode}");
            }

            await CopyLimitedAsync(response, candidatePath, _options.MaxDownloadBytes, host, timeout.Token);
            var sha256 = await DatFile.Sha256Async(candidatePath, cancellationToken);
            var revision = new DomainSourceRevision(
                null,
                response.Headers.ETag?.ToString(),
                response.Content.Headers.LastModified?.ToString("R"),
                sha256);

            if (dat.ChecksumUrl is { } checksumUrl)
            {
                EnsureChecksum(await client.GetStringAsync(checksumUrl, timeout.Token), sha256, "контрольная сумма");
            }

            return previous?.Sha256 == sha256 ? previous with { ETag = revision.ETag, LastModified = revision.LastModified } : revision;
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException($"{host} недоступен: {ex.HttpRequestError}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException($"Загрузка с {host} не уложилась в {_options.DownloadTimeoutSeconds} с", ex);
        }
    }

    private static async Task CopyLimitedAsync(HttpResponseMessage response, string path, long maxBytes, string host, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw TooLarge(host, maxBytes);
        }

        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var destination = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[CopyBufferSize];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > maxBytes)
            {
                throw TooLarge(host, maxBytes);
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void EnsureChecksum(string checksumContent, string actualSha256, string checksumName)
    {
        string expected;
        try
        {
            expected = Sha256Checksum.Parse(checksumContent);
        }
        catch (FormatException ex)
        {
            throw new ExternalServiceUnavailableException($"Не удалось прочитать {checksumName}: {ex.Message}", ex);
        }

        if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(actualSha256)))
        {
            throw new ExternalServiceUnavailableException($"SHA-256 не совпал с {checksumName}: файл повреждён при загрузке");
        }
    }

    private static ExternalServiceUnavailableException TooLarge(string host, long maxBytes) =>
        new($"Файл с {host} больше допустимых {maxBytes / 1024 / 1024} МиБ");
}
