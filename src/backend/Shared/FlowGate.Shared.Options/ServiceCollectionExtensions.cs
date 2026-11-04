using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using FlowGate.Shared.Options.Validation;

namespace FlowGate.Shared.Options;

/// <summary>
///     Регистрация опций FlowGate
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует <see cref="FlowGateOptions"/> и <see cref="KeeneticOptions"/> с валидацией при старте
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="configuration">Конфигурация приложения</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<FlowGateOptions>, FlowGateOptionsValidator>();
        services.AddSingleton<IValidateOptions<KeeneticOptions>, KeeneticOptionsValidator>();

        services.AddOptionsWithValidateOnStart<FlowGateOptions>()
            .Bind(configuration.GetSection(FlowGateOptions.SectionName));

        services.AddOptionsWithValidateOnStart<KeeneticOptions>()
            .Bind(configuration.GetSection(KeeneticOptions.SectionName));

        return services;
    }
}
