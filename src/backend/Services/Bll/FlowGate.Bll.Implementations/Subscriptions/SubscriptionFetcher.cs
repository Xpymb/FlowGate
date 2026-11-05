using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Subscriptions;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Subscriptions;

/// <summary>
///     Загрузка подписки. Перенаправления обрабатываются вручную (до 3): HTTPS→HTTP запрещён, сведения об устройстве
///     отправляются только на исходный origin
/// </summary>
public sealed partial class SubscriptionFetcher : ISubscriptionFetcher
{
    /// <summary>
    ///     Имя HTTP-клиента подписок
    /// </summary>
    public const string HttpClientName = "subscriptions";

    private const int MaxRedirects = 3;
    private const int CopyBufferSize = 81920;

    private static readonly string[] HwidRefusalFlags = ["x-hwid-max-devices-reached", "x-hwid-limit", "x-hwid-not-supported"];

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SubscriptionFetcher> _logger;
    private readonly SubscriptionOptions _options;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionFetcher"/>
    /// </summary>
    /// <param name="httpClientFactory">Фабрика HTTP-клиентов</param>
    /// <param name="scopeFactory">Фабрика scope для чтения сведений о роутере</param>
    /// <param name="options">Настройки подписок</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SubscriptionFetcher(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<SubscriptionOptions> options,
        ILogger<SubscriptionFetcher> logger)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<SubscriptionDownload> FetchAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        var origin = new Uri(subscription.Url, UriKind.Absolute);
        var deviceHeaders = subscription.SendDeviceInfo ? await GetDeviceHeadersAsync(cancellationToken) : [];

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));

        var client = _httpClientFactory.CreateClient(HttpClientName);
        var current = origin;
        for (var redirect = 0; ; redirect++)
        {
            using var request = CreateRequest(subscription, current, SameOrigin(origin, current) ? deviceHeaders : []);
            using var response = await SendAsync(client, request, origin.Host, timeout.Token, cancellationToken);

            EnsureHwidAccepted(response);

            if (IsRedirect(response.StatusCode))
            {
                current = NextLocation(current, response, redirect);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new ExternalServiceUnavailableException($"Провайдер {origin.Host} ответил HTTP {(int)response.StatusCode}");
            }

            var body = await ReadBodyAsync(response, origin.Host, timeout.Token, cancellationToken);
            return new SubscriptionDownload(body, CollectHeaders(response));
        }
    }

    private static HttpRequestMessage CreateRequest(Subscription subscription, Uri uri, IReadOnlyList<(string Name, string Value)> deviceHeaders)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.TryAddWithoutValidation("User-Agent", string.IsNullOrWhiteSpace(subscription.UserAgent) ? AppVersion.UserAgent : subscription.UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");

        if (subscription.SendHwid)
        {
            request.Headers.TryAddWithoutValidation("x-hwid", subscription.Hwid);
        }

        foreach (var (name, value) in deviceHeaders)
        {
            request.Headers.TryAddWithoutValidation(name, value);
        }

        return request;
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpRequestMessage request,
        string host,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        try
        {
            return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ExternalServiceUnavailableException($"Провайдер {host} недоступен: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!callerToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException($"Провайдер {host} не ответил вовремя", ex);
        }
    }

    private static void EnsureHwidAccepted(HttpResponseMessage response)
    {
        var flag = HwidRefusalFlags.FirstOrDefault(name =>
            response.Headers.TryGetValues(name, out var values) && values.Any(value => value.Trim() is "true" or "1"));

        if (flag is not null)
        {
            var reason = flag switch
            {
                "x-hwid-max-devices-reached" => "достигнут лимит устройств",
                "x-hwid-not-supported" => "провайдер не поддерживает HWID",
                _ => "превышен лимит устройств",
            };
            throw new ExternalServiceUnavailableException($"Провайдер отклонил устройство (HWID): {reason}");
        }
    }

    private static bool IsRedirect(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found or HttpStatusCode.SeeOther
            or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    private static Uri NextLocation(Uri current, HttpResponseMessage response, int redirect)
    {
        if (redirect >= MaxRedirects)
        {
            throw new ExternalServiceUnavailableException($"Провайдер {current.Host}: больше {MaxRedirects} перенаправлений");
        }

        var location = response.Headers.Location
            ?? throw new ExternalServiceUnavailableException($"Провайдер {current.Host}: перенаправление без адреса");
        var next = location.IsAbsoluteUri ? location : new Uri(current, location);

        if (next.Scheme is not ("http" or "https"))
        {
            throw new ExternalServiceUnavailableException("Перенаправление подписки на неподдерживаемую схему");
        }

        if (current.Scheme == "https" && next.Scheme == "http")
        {
            throw new ExternalServiceUnavailableException("Перенаправление подписки с HTTPS на HTTP запрещено");
        }

        return next;
    }

    private async Task<string> ReadBodyAsync(
        HttpResponseMessage response,
        string host,
        CancellationToken timeoutToken,
        CancellationToken callerToken)
    {
        if (response.Content.Headers.ContentLength > _options.MaxResponseBytes)
        {
            throw TooLarge(host);
        }

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[CopyBufferSize];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeoutToken)) > 0)
            {
                if (buffer.Length + read > _options.MaxResponseBytes)
                {
                    throw TooLarge(host);
                }

                buffer.Write(chunk, 0, read);
            }

            return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
        }
        catch (Exception ex) when (ex is IOException or HttpRequestException)
        {
            throw new ExternalServiceUnavailableException($"Не удалось прочитать ответ провайдера {host}: {ex.Message}", ex);
        }
        catch (OperationCanceledException ex) when (!callerToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException($"Провайдер {host} не ответил вовремя", ex);
        }
    }

    private ExternalServiceUnavailableException TooLarge(string host) =>
        new($"Ответ провайдера {host} больше {_options.MaxResponseBytes / 1024 / 1024} МиБ");

    private static Dictionary<string, string> CollectHeaders(HttpResponseMessage response) =>
        response.Headers
            .Concat(response.Content.Headers)
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key.ToLowerInvariant(),
                group => string.Join(", ", group.SelectMany(header => header.Value)),
                StringComparer.OrdinalIgnoreCase);

    private async Task<IReadOnlyList<(string Name, string Value)>> GetDeviceHeadersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var info = await scope.ServiceProvider.GetRequiredService<IKeeneticSystemService>().GetSystemInfoAsync(cancellationToken);

            var headers = new List<(string, string)> { ("X-Device-OS", "KeeneticOS"), ("X-App-Version", AppVersion.Current) };
            if (ModelIndex().IsMatch(info.HardwareId))
            {
                headers.Add(("X-Device-Model", info.HardwareId));
            }

            if (!string.IsNullOrWhiteSpace(info.Title))
            {
                headers.Add(("X-Ver-OS", info.Title));
            }

            return headers;
        }
        catch (FlowGateException ex)
        {
            _logger.LogWarning("Сведения о роутере для подписки не получены: {Error}", ex.Message);
            return [("X-App-Version", AppVersion.Current)];
        }
    }

    private static bool SameOrigin(Uri left, Uri right) =>
        left.Scheme == right.Scheme
        && string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase)
        && left.Port == right.Port;

    [GeneratedRegex("^KN-[0-9]{3,5}$")]
    private static partial Regex ModelIndex();
}
