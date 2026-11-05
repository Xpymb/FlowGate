using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Maintenance;
using FlowGate.Shared.Options;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Периодическая проверка новых версий FlowGate (только уведомление, обновляет пользователь)
/// </summary>
public sealed class SystemUpdateCheckJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(2);

    private readonly ISystemService _system;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SystemUpdateCheckJob> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SystemUpdateCheckJob"/>
    /// </summary>
    /// <param name="system">Состояние и обновление FlowGate</param>
    /// <param name="options">Настройки FlowGate (интервал проверки)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SystemUpdateCheckJob(
        ISystemService system,
        IOptions<SystemOptions> options,
        TimeProvider timeProvider,
        ILogger<SystemUpdateCheckJob> logger)
    {
        _system = system ?? throw new ArgumentNullException(nameof(system));
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = TimeSpan.FromHours(options.Value.UpdateCheckIntervalHours);
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
            await _system.CheckForUpdatesAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Проверка обновлений FlowGate не удалась: {Error}", ex.Message);
        }
    }
}
