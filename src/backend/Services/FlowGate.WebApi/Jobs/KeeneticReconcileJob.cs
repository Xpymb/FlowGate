using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Connections;
using FlowGate.Bll.Interfaces.DnsRoutes;
using FlowGate.Shared.Options;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Сверка объектов FlowGate в Keenetic: вскоре после старта и затем периодически. Сначала интерфейсы соединений,
///     затем DNS-маршруты: правила на пересозданные интерфейсы Keenetic удалил сам, и они возвращаются с прежним флагом
/// </summary>
public sealed class KeeneticReconcileJob : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    private readonly IConnectionService _connections;
    private readonly IDnsRouteService _routes;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<KeeneticReconcileJob> _logger;
    private readonly TimeSpan _interval;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticReconcileJob"/>
    /// </summary>
    /// <param name="connections">Соединения</param>
    /// <param name="routes">DNS-маршруты</param>
    /// <param name="options">Настройки соединений (период сверки)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public KeeneticReconcileJob(
        IConnectionService connections,
        IDnsRouteService routes,
        IOptions<ConnectionOptions> options,
        TimeProvider timeProvider,
        ILogger<KeeneticReconcileJob> logger)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _interval = TimeSpan.FromMinutes(options.Value.ReconcileIntervalMinutes);
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
            var interfaces = await _connections.ReconcileAsync(stoppingToken);
            if (interfaces.Repaired > 0)
            {
                _logger.LogInformation("Сверка Keenetic: исправлено интерфейсов {Count}", interfaces.Repaired);
            }

            var commands = await _routes.ReconcileAsync(interfaces.RecreatedInterfaces.ToHashSet(StringComparer.OrdinalIgnoreCase), stoppingToken);
            if (commands > 0)
            {
                _logger.LogInformation("Сверка Keenetic: DNS-маршруты исправлены, команд {Count}", commands);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning("Сверка Keenetic не удалась: {Error}", ex.Message);
        }
    }
}
