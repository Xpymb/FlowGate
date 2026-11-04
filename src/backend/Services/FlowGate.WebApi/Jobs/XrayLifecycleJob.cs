using FlowGate.Bll.Interfaces.Xray;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Запуск Xray вместе с FlowGate и остановка при выключении. Старт не блокирует запуск веб-сервера
/// </summary>
public sealed class XrayLifecycleJob : IHostedService
{
    private readonly IXrayService _xray;
    private readonly ILogger<XrayLifecycleJob> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _initialization;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayLifecycleJob"/>
    /// </summary>
    /// <param name="xray">Управление Xray</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayLifecycleJob(IXrayService xray, ILogger<XrayLifecycleJob> logger)
    {
        _xray = xray ?? throw new ArgumentNullException(nameof(xray));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _initialization = Task.Run(InitializeAsync, CancellationToken.None);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();

        if (_initialization is not null)
        {
            await _initialization.WaitAsync(cancellationToken);
        }

        await _xray.ShutdownAsync(cancellationToken);
    }

    private async Task InitializeAsync()
    {
        try
        {
            await _xray.InitializeAsync(_stopping.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка запуска Xray при старте FlowGate");
        }
    }
}
