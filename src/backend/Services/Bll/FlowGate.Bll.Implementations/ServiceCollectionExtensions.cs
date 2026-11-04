using Microsoft.Extensions.DependencyInjection;

using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Interfaces.Keenetic;

namespace FlowGate.Bll.Implementations;

/// <summary>
///     Регистрация сервисов BLL
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует сервисы BLL
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateBll(this IServiceCollection services)
    {
        services.AddScoped<IKeeneticSystemService, KeeneticSystemService>();
        return services;
    }
}
