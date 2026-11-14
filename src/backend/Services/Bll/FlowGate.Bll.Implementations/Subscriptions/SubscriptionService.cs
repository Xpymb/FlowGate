using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Implementations.Settings;
using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Bll.Interfaces.Subscriptions;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Dal.Interfaces.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Servers;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.SharedContracts.Dto.Subscriptions;

namespace FlowGate.Bll.Implementations.Subscriptions;

/// <summary>
///     Подписки: хранение, обновление фоновой задачей, планировщик
/// </summary>
public sealed class SubscriptionService : ISubscriptionService
{
    /// <summary>
    ///     Префикс вида фоновой задачи обновления; полный вид — <c>subscription-refresh:&lt;id&gt;</c>
    /// </summary>
    public const string JobKindPrefix = "subscription-refresh:";

    private const string EntityName = "Подписка";
    private const int MaxNameLength = 128;
    private const int MaxUrlLength = 4096;
    private const int MaxUserAgentLength = 128;

    private readonly ISubscriptionProvider _subscriptions;
    private readonly IServerProvider _servers;
    private readonly ISubscriptionFetcher _fetcher;
    private readonly IServerImportParser _parser;
    private readonly IJobRunner _jobs;
    private readonly IXrayConfigService _xrayConfig;
    private readonly ISettingsProvider _settings;
    private readonly IUpdateProxyService _updateProxy;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly SubscriptionOptions _options;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionService"/>
    /// </summary>
    /// <param name="subscriptions">Хранилище подписок</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="fetcher">Загрузка подписки</param>
    /// <param name="parser">Разбор содержимого</param>
    /// <param name="jobs">Фоновые задачи</param>
    /// <param name="xrayConfig">Применение конфигурации Xray после изменения серверов</param>
    /// <param name="settings">Пользовательские настройки (интервал по умолчанию)</param>
    /// <param name="updateProxy">Прокси обновлений</param>
    /// <param name="options">Настройки подписок</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SubscriptionService(
        ISubscriptionProvider subscriptions,
        IServerProvider servers,
        ISubscriptionFetcher fetcher,
        IServerImportParser parser,
        IJobRunner jobs,
        IXrayConfigService xrayConfig,
        ISettingsProvider settings,
        IUpdateProxyService updateProxy,
        IOptions<SubscriptionOptions> options,
        TimeProvider timeProvider,
        ILogger<SubscriptionService> logger)
    {
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _fetcher = fetcher ?? throw new ArgumentNullException(nameof(fetcher));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _xrayConfig = xrayConfig ?? throw new ArgumentNullException(nameof(xrayConfig));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _updateProxy = updateProxy ?? throw new ArgumentNullException(nameof(updateProxy));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private async Task<TimeSpan> DefaultIntervalAsync(CancellationToken cancellationToken) =>
        UpdateIntervals.SubscriptionDefault(await _settings.GetAsync(cancellationToken), _options);

    private TimeSpan RetryAfterFailure => TimeSpan.FromMinutes(_options.RetryAfterFailureMinutes);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var counts = await CountServersAsync(cancellationToken);
        var interval = await DefaultIntervalAsync(cancellationToken);
        var subscriptions = await _subscriptions.GetAllAsync(cancellationToken);
        return subscriptions.Select(subscription => ToDto(subscription, counts, interval)).ToList();
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var subscription = await GetExistingAsync(id, cancellationToken);
        return ToDto(subscription, await CountServersAsync(cancellationToken), await DefaultIntervalAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto> CreateAsync(SubscriptionSaveRequest request, CancellationToken cancellationToken)
    {
        var valid = Validate(request);
        var subscription = new Subscription(
            Guid.NewGuid().ToString("N")[..12],
            valid.Name,
            valid.Url,
            request.AutoUpdate,
            request.UpdateIntervalHours,
            valid.UserAgent,
            request.SendHwid,
            Guid.NewGuid().ToString(),
            request.SendDeviceInfo,
            null,
            null,
            _timeProvider.GetUtcNow());

        await _subscriptions.AddAsync(subscription, cancellationToken);
        _logger.LogInformation("Подписка {SubscriptionId} ({Host}) добавлена", subscription.Id, subscription.DisplayName);

        var job = StartRefresh(subscription);
        return subscription.ToDto(0, await DefaultIntervalAsync(cancellationToken), RetryAfterFailure, job.Id);
    }

    /// <inheritdoc />
    public async Task<ServerPreviewDto> PreviewAsync(SubscriptionSaveRequest request, CancellationToken cancellationToken)
    {
        var valid = Validate(request);
        var draft = new Subscription(
            "preview",
            valid.Name,
            valid.Url,
            AutoUpdate: false,
            null,
            valid.UserAgent,
            request.SendHwid,
            Guid.NewGuid().ToString(),
            request.SendDeviceInfo,
            null,
            null,
            _timeProvider.GetUtcNow());

        ServerPreviewDto? preview = null;
        await UpdateRoute.RunAsync(
            _updateProxy,
            UpdateVia.Auto,
            null,
            async () =>
            {
                var download = await _fetcher.FetchAsync(draft, cancellationToken);
                var batch = _parser.Parse(download.Body);
                var metadata = ProviderMetadataParser.Parse(download.Headers, batch.BodyMetadata);
                preview = batch.ToPreviewDto(metadata?.Title);
            },
            cancellationToken);

        _logger.LogInformation("Предпросмотр подписки {Host}: серверов {Servers}, отклонено {Rejected}", new Uri(draft.Url).Host, preview!.Servers.Count, preview.Rejected.Count);
        return preview;
    }

    /// <inheritdoc />
    public async Task<SubscriptionDto> UpdateAsync(string id, SubscriptionSaveRequest request, CancellationToken cancellationToken)
    {
        var valid = Validate(request);
        var updated = await _subscriptions.UpdateAsync(
            id,
            existing => existing with
            {
                Name = valid.Name,
                Url = valid.Url,
                AutoUpdate = request.AutoUpdate,
                UpdateIntervalHours = request.UpdateIntervalHours,
                UserAgent = valid.UserAgent,
                SendHwid = request.SendHwid,
                SendDeviceInfo = request.SendDeviceInfo,
                Provider = existing.Url == valid.Url ? existing.Provider : null,
            },
            cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

        return ToDto(updated, await CountServersAsync(cancellationToken), await DefaultIntervalAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var subscription = await GetExistingAsync(id, cancellationToken);

        await _servers.ReplaceSubscriptionServersAsync(id, [], cancellationToken);
        await _subscriptions.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Подписка {SubscriptionId} «{Name}» удалена вместе с её серверами", id, subscription.DisplayName);

        await ApplyXrayConfigAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<JobDto> RefreshAsync(string id, UpdateVia via, CancellationToken cancellationToken)
    {
        var subscription = await GetExistingAsync(id, cancellationToken);
        return StartRefresh(subscription, via).ToDto();
    }

    /// <inheritdoc />
    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var interval = await DefaultIntervalAsync(cancellationToken);
        var due = (await _subscriptions.GetAllAsync(cancellationToken))
            .Where(subscription => subscription.NextRefreshAt(interval, RetryAfterFailure) <= now)
            .ToList();

        var refreshed = 0;
        foreach (var subscription in due)
        {
            JobSnapshot job;
            try
            {
                job = StartRefresh(subscription);
            }
            catch (ConflictException)
            {
                continue;
            }

            await _jobs.WaitAsync(job.Id, cancellationToken);
            refreshed++;
        }

        return refreshed;
    }

    private JobSnapshot StartRefresh(Subscription subscription, UpdateVia via = UpdateVia.Auto) =>
        _jobs.Start(
            JobKindPrefix + subscription.Id,
            $"Обновление подписки «{subscription.DisplayName}»",
            (context, token) => UpdateRoute.RunAsync(_updateProxy, via, context, () => RefreshCoreAsync(subscription.Id, context, token), token));

    private async Task RefreshCoreAsync(string id, IJobContext context, CancellationToken cancellationToken)
    {
        var subscription = await GetExistingAsync(id, cancellationToken);
        var host = new Uri(subscription.Url).Host;

        SubscriptionRefreshResult result;
        try
        {
            context.Report($"Загрузка подписки с {host}");
            var download = await _fetcher.FetchAsync(subscription, cancellationToken);

            context.Report("Разбор ответа");
            var batch = _parser.Parse(download.Body);
            var metadata = ProviderMetadataParser.Parse(download.Headers, batch.BodyMetadata);

            context.Report("Синхронизация серверов");
            var existing = (await _servers.GetAllAsync(cancellationToken)).Where(server => server.SubscriptionId == id).ToList();
            var outcome = SubscriptionSync.Apply(id, existing, batch, _timeProvider.GetUtcNow(), _options.MaxServersPerSubscription);
            result = outcome.Result;

            if (result.Status == SubscriptionRefreshStatus.Failed)
            {
                await _subscriptions.UpdateAsync(id, current => current with { LastRefresh = result }, cancellationToken);
                throw new ExternalServiceUnavailableException(result.Error ?? "Обновление подписки не удалось");
            }

            await _servers.ReplaceSubscriptionServersAsync(id, outcome.Servers, cancellationToken);
            await _subscriptions.UpdateAsync(id, current => current with { Provider = metadata, LastRefresh = result }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await RecordFailureAsync(id, ex.Message);
            throw;
        }

        var summary = $"принято {result.Accepted}, добавлено {result.Added}, изменено {result.Updated}, удалено {result.Removed}"
            + (result.Retained > 0 ? $", сохранено {result.Retained}" : string.Empty);
        context.Report($"Готово: {summary}");
        _logger.LogInformation("Подписка {SubscriptionId} «{Name}» обновлена ({Status}): {Summary}", id, subscription.DisplayName, result.Status, summary);

        await ApplyXrayConfigAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(string id, string error)
    {
        await _subscriptions.UpdateAsync(
            id,
            current => current.LastRefresh?.Status == SubscriptionRefreshStatus.Failed && current.LastRefresh.Error == error
                ? current
                : current with { LastRefresh = SubscriptionRefreshResult.Failure(_timeProvider.GetUtcNow(), error) },
            CancellationToken.None);
    }

    private async Task ApplyXrayConfigAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _xrayConfig.ApplyAsync(cancellationToken);
        }
        catch (XrayOperationException ex)
        {
            _logger.LogWarning("Конфигурация Xray после изменения серверов не применена: {Error}", ex.Message);
        }
    }

    private async Task<Subscription> GetExistingAsync(string id, CancellationToken cancellationToken) =>
        await _subscriptions.GetAsync(id, cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

    private async Task<Dictionary<string, int>> CountServersAsync(CancellationToken cancellationToken) =>
        (await _servers.GetAllAsync(cancellationToken))
            .Where(server => server.SubscriptionId is not null)
            .GroupBy(server => server.SubscriptionId!)
            .ToDictionary(group => group.Key, group => group.Count());

    private SubscriptionDto ToDto(Subscription subscription, Dictionary<string, int> counts, TimeSpan defaultInterval) =>
        subscription.ToDto(counts.GetValueOrDefault(subscription.Id), defaultInterval, RetryAfterFailure);

    private static (string Url, string? Name, string? UserAgent) Validate(SubscriptionSaveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = request.Url?.Trim() ?? string.Empty;
        if (url.Length is 0 or > MaxUrlLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            throw new RequestValidationException("Адрес подписки должен быть ссылкой http:// или https://");
        }

        var name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        if (name is { Length: > MaxNameLength })
        {
            throw new RequestValidationException($"Имя подписки не длиннее {MaxNameLength} символов");
        }

        if (request.UpdateIntervalHours is < 1 or > 168)
        {
            throw new RequestValidationException("Интервал обновления — от 1 до 168 часов");
        }

        var userAgent = string.IsNullOrWhiteSpace(request.UserAgent) ? null : request.UserAgent.Trim();
        if (userAgent is not null && (userAgent.Length > MaxUserAgentLength || userAgent.Any(symbol => symbol is < ' ' or > '~')))
        {
            throw new RequestValidationException($"User-Agent — печатные символы ASCII, не длиннее {MaxUserAgentLength}");
        }

        return (url, name, userAgent);
    }
}
