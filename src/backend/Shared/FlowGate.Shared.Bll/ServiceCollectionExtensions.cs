using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Changes;

namespace FlowGate.Shared.Bll;

/// <summary>
///     Регистрация общей BLL-инфраструктуры
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует очередь NDMS, запуск процессов, фоновые задачи, общую <see cref="IChangeFeed"/> и <see cref="TimeProvider"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateSharedBll(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IChangeFeed, ChangeFeed>();
        services.AddSingleton<INdmsOperationQueue, NdmsOperationQueue>();
        services.AddSingleton<IProcessRunner, ProcessRunner>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IProcessTable, ProcFsProcessTable>();
        services.AddSingleton<IJobRunner, JobRunner>();
        return services;
    }
}
