using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Shared.Options;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Планировщик источников списков: периодически обновляет используемые источники, которым пора
/// </summary>
public sealed class DomainSourceRefreshJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(3);

    private readonly IDomainSourceService _sources;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DomainSourceRefreshJob> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainSourceRefreshJob"/>
    /// </summary>
    /// <param name="sources">Источники</param>
    /// <param name="options">Настройки источников (период планировщика)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public DomainSourceRefreshJob(
        IDomainSourceService sources,
        IOptions<DomainSourceOptions> options,
        TimeProvider timeProvider,
        ILogger<DomainSourceRefreshJob> logger)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
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
            await _sources.RefreshDueAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Плановое обновление источников не удалось: {Error}", ex.Message);
        }
    }
}
