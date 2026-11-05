using Microsoft.Extensions.DependencyInjection;

using FlowGate.Shared.Dal.Json;

namespace FlowGate.Shared.Dal;

/// <summary>
///     Регистрация общей DAL-инфраструктуры
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует <see cref="IJsonFileStoreFactory"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateSharedDal(this IServiceCollection services)
    {
        services.AddSingleton<IJsonFileStoreFactory, JsonFileStoreFactory>();
        return services;
    }
}
