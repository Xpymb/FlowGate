using Microsoft.Extensions.DependencyInjection;

using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Jobs;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Xray;

namespace FlowGate.Bll.Implementations;

/// <summary>
///     Регистрация сервисов BLL
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует сервисы BLL. Сервисы Xray — синглтоны: процесс, кэш релизов и буфер логов живут всё время работы приложения
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateBll(this IServiceCollection services)
    {
        services.AddScoped<IKeeneticSystemService, KeeneticSystemService>();

        services.AddSingleton<IJobService, JobService>();

        services.AddSingleton<IXrayLogBuffer, XrayLogBuffer>();
        services.AddSingleton<IXrayVersionProvider, XrayVersionProvider>();
        services.AddSingleton<IXrayProcessService, XrayProcessService>();
        services.AddSingleton<IXrayConfigBuilder, XrayConfigBuilder>();
        services.AddSingleton<IXrayConfigSource, EmptyXrayConfigSource>();
        services.AddSingleton<IXrayConfigService, XrayConfigService>();
        services.AddSingleton<IXrayReleaseService, XrayReleaseService>();
        services.AddSingleton<IXrayInstaller, XrayInstaller>();
        services.AddSingleton<IXrayService, XrayService>();

        return services;
    }
}
