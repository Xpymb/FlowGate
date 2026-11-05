using FlowGate.Bll.Interfaces.Maintenance;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Проверка новых версий FlowGate (только уведомление, обновляет пользователь): раз в час смотрит, истёк ли интервал из настроек
/// </summary>
public sealed class SystemUpdateCheckJob : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    private readonly ISystemService _system;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SystemUpdateCheckJob> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SystemUpdateCheckJob"/>
    /// </summary>
    /// <param name="system">Состояние и обновление FlowGate</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SystemUpdateCheckJob(
        ISystemService system,
        TimeProvider timeProvider,
        ILogger<SystemUpdateCheckJob> logger)
    {
        _system = system ?? throw new ArgumentNullException(nameof(system));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(InitialDelay, _timeProvider, stoppingToken);

            using var timer = new PeriodicTimer(TickInterval, _timeProvider);
            do
            {
                await CheckAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task CheckAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _system.CheckForUpdatesIfDueAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Проверка обновлений FlowGate не удалась: {Error}", ex.Message);
        }
    }
}
