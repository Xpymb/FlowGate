using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.UpdateProxy;

/// <summary>
///     Выполнение операции обновления в одной <see cref="UpdateProxyScope"/>: прокси поднимается на всю операцию и
///     гасится сразу после неё
/// </summary>
public static class UpdateRoute
{
    /// <summary>
    ///     Выполняет действие в области обновления и пишет в задачу, каким путём идёт загрузка
    /// </summary>
    /// <param name="proxy">Прокси обновлений</param>
    /// <param name="via">Путь</param>
    /// <param name="context">Задача для сообщения о пути или null</param>
    /// <param name="action">Действие</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Задача выполнения</returns>
    /// <exception cref="FlowGate.Exceptions.ExternalServiceUnavailableException">Если выбран <see cref="UpdateVia.Proxy"/>, а прокси недоступен</exception>
    public static async Task RunAsync(
        IUpdateProxyService proxy,
        UpdateVia via,
        IJobContext? context,
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentNullException.ThrowIfNull(action);

        using var scope = UpdateProxyScope.Enter(via, await proxy.AcquireAsync(via, cancellationToken));
        context?.Report(scope.Lease is null ? "Загрузка напрямую" : "Загрузка через прокси обновлений");
        await action();
    }
}
