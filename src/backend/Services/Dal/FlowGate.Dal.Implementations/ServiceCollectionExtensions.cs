using Microsoft.Extensions.DependencyInjection;

using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Dal.Implementations.Subscriptions;
using FlowGate.Dal.Implementations.Xray;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Dal.Interfaces.Subscriptions;
using FlowGate.Dal.Interfaces.Xray;

namespace FlowGate.Dal.Implementations;

/// <summary>
///     Регистрация провайдеров хранения
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует провайдеры хранения; все синглтоны, потому что держат по одному файлу
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateDal(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsProvider, SettingsProvider>();
        services.AddSingleton<IXrayConfigFileProvider, XrayConfigFileProvider>();
        services.AddSingleton<IServerProvider, ServerProvider>();
        services.AddSingleton<ISubscriptionProvider, SubscriptionProvider>();
        return services;
    }
}
