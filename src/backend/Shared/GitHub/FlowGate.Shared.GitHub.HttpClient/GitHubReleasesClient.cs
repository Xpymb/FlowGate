using System.Net;
using System.Text.Json;

using Microsoft.Extensions.Options;

using FlowGate.Exceptions;
using FlowGate.Shared.GitHub.Models;
using FlowGate.Shared.GitHub.Serialization;
using FlowGate.Shared.Options;

namespace FlowGate.Shared.GitHub;

/// <summary>
///     Клиент релизов GitHub поверх <see cref="HttpClient"/>. Таймауты задаются на каждый запрос, у самого клиента их нет
/// </summary>
public sealed class GitHubReleasesClient : IGitHubReleasesClient
{
    private const int CopyBufferSize = 81920;

    private readonly HttpClient _httpClient;
    private readonly TimeSpan _requestTimeout;
    private readonly TimeSpan _downloadTimeout;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="GitHubReleasesClient"/>
    /// </summary>
    /// <param name="httpClient">HTTP-клиент с базовым адресом API</param>
    /// <param name="options">Настройки GitHub</param>
    /// <exception cref="ArgumentNullException">Если httpClient или options равны null</exception>
    public GitHubReleasesClient(HttpClient httpClient, IOptions<GitHubOptions> options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        ArgumentNullException.ThrowIfNull(options);
        _requestTimeout = TimeSpan.FromSeconds(options.Value.RequestTimeoutSeconds);
        _downloadTimeout = TimeSpan.FromSeconds(options.Value.DownloadTimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<GitHubRelease>> GetReleasesAsync(
        string repository,
        int count,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repository);
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(count, 100);

        var path = $"repos/{repository.Trim('/')}/releases?per_page={count}";
        using var timeout = CreateTimeout(_requestTimeout, cancellationToken);
        using var response = await SendAsync(new Uri(path, UriKind.Relative), $"релизы {repository}", timeout.Token, cancellationToken);

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            return await JsonSerializer.DeserializeAsync(stream, GitHubJsonContext.Default.ListGitHubRelease, timeout.Token)
                ?? [];
        }
        catch (JsonException ex)
        {
            throw new ExternalServiceUnavailableException($"GitHub вернул некорректный список релизов {repository}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<long> DownloadAsync(Uri url, string destinationPath, long maxBytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var fileName = Path.GetFileName(url.AbsolutePath);
        using var timeout = CreateTimeout(_downloadTimeout, cancellationToken);
        using var response = await SendAsync(url, fileName, timeout.Token, cancellationToken);

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            throw TooLarge(fileName, maxBytes);
        }

        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            await using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[CopyBufferSize];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
            {
                total += read;
                if (total > maxBytes)
                {
                    throw TooLarge(fileName, maxBytes);
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            }

            return total;
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new ExternalServiceUnavailableException($"Не удалось скачать {fileName}: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException($"Скачивание {fileName} не уложилось в {_downloadTimeout.TotalSeconds:0} с", ex);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        Uri uri,
        string description,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeoutToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException($"GitHub недоступен ({description}): {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!callerToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException($"GitHub не ответил вовремя ({description})", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        var message = IsRateLimited(response)
            ? "Исчерпан лимит запросов к GitHub API (60 в час без токена), повторите позже"
            : $"GitHub ответил HTTP {(int)response.StatusCode} ({description})";
        response.Dispose();
        throw new ExternalServiceUnavailableException(message);
    }

    private static bool IsRateLimited(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests
        && response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
        && remaining.FirstOrDefault() == "0";

    private static CancellationTokenSource CreateTimeout(TimeSpan timeout, CancellationToken cancellationToken)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);
        return source;
    }

    private static ExternalServiceUnavailableException TooLarge(string fileName, long maxBytes) =>
        new($"Файл {fileName} больше допустимых {maxBytes / 1024 / 1024} МиБ");
}
