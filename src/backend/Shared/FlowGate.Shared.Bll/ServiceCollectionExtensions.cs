using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Shared.Bll;

/// <summary>
///     Регистрация общей BLL-инфраструктуры
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует очередь NDMS, запуск процессов, фоновые задачи и <see cref="TimeProvider"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateSharedBll(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<INdmsOperationQueue, NdmsOperationQueue>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IJobRunner, JobRunner>();
        return services;
    }
}
