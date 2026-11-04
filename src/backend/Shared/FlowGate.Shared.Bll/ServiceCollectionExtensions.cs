using Microsoft.Extensions.DependencyInjection;

using FlowGate.Shared.Bll.Ndms;

namespace FlowGate.Shared.Bll;

/// <summary>
///     Регистрация общей BLL-инфраструктуры
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует <see cref="INdmsOperationQueue"/> одним экземпляром на приложение
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateSharedBll(this IServiceCollection services)
    {
        services.AddSingleton<INdmsOperationQueue, NdmsOperationQueue>();
        return services;
    }
}
