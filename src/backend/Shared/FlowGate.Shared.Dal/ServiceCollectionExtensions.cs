using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using FlowGate.Shared.Changes;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Shared.Dal;

/// <summary>
///     Регистрация общей DAL-инфраструктуры
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует <see cref="IJsonFileStoreFactory"/> и общую <see cref="IChangeFeed"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateSharedDal(this IServiceCollection services)
    {
        services.TryAddSingleton<IChangeFeed, ChangeFeed>();
        services.AddSingleton<IJsonFileStoreFactory, JsonFileStoreFactory>();
        return services;
    }
}
