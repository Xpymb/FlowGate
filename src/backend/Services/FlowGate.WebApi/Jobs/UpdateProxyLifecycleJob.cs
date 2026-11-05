using FlowGate.Bll.Interfaces.UpdateProxy;

namespace FlowGate.WebApi.Jobs;

/// <summary>
///     Остановка прокси обновлений вместе с FlowGate
/// </summary>
public sealed class UpdateProxyLifecycleJob : IHostedService
{
    private readonly IUpdateProxyService _proxy;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyLifecycleJob"/>
    /// </summary>
    /// <param name="proxy">Прокси обновлений</param>
    /// <exception cref="ArgumentNullException">Если proxy равен null</exception>
    public UpdateProxyLifecycleJob(IUpdateProxyService proxy)
    {
        _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => _proxy.ShutdownAsync(cancellationToken);
}
