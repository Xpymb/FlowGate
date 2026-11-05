using FlowGate.Bll.Interfaces.Xray;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Проверка новых релизов Xray (только уведомление, без установки): раз в час смотрит, истёк ли интервал из настроек
/// </summary>
public sealed class XrayUpdateCheckJob : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);

    private readonly IXrayService _xray;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<XrayUpdateCheckJob> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayUpdateCheckJob"/>
    /// </summary>
    /// <param name="xray">Управление Xray</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayUpdateCheckJob(
        IXrayService xray,
        TimeProvider timeProvider,
        ILogger<XrayUpdateCheckJob> logger)
    {
        _xray = xray ?? throw new ArgumentNullException(nameof(xray));
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
            await _xray.CheckForUpdatesIfDueAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Проверка обновлений Xray не удалась: {Error}", ex.Message);
        }
    }
}
