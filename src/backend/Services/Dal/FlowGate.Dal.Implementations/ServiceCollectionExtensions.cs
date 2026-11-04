using Microsoft.Extensions.DependencyInjection;

using FlowGate.Dal.Implementations.Settings;
using FlowGate.Dal.Implementations.Xray;
using FlowGate.Dal.Interfaces.Settings;
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
        return services;
    }
}
