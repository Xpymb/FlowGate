using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Subscriptions;
using FlowGate.Shared.Options;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Планировщик подписок: периодически обновляет те, которым пора
/// </summary>
public sealed class SubscriptionRefreshJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    private readonly ISubscriptionService _subscriptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SubscriptionRefreshJob> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionRefreshJob"/>
    /// </summary>
    /// <param name="subscriptions">Подписки</param>
    /// <param name="options">Настройки подписок (период планировщика)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SubscriptionRefreshJob(
        ISubscriptionService subscriptions,
        IOptions<SubscriptionOptions> options,
        TimeProvider timeProvider,
        ILogger<SubscriptionRefreshJob> logger)
    {
        _subscriptions = subscriptions ?? throw new ArgumentNullException(nameof(subscriptions));
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = TimeSpan.FromMinutes(options.Value.SchedulerIntervalMinutes);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, _timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(_interval, _timeProvider);
            do
            {
                await TickAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task TickAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _subscriptions.RefreshDueAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Плановое обновление подписок не удалось: {Error}", ex.Message);
        }
    }
}
