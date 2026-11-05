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
    ///     Регистрирует все опции FlowGate с валидацией при старте
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <param name="configuration">Конфигурация приложения</param>
    /// <returns>Та же коллекция сервисов</returns>
    public static IServiceCollection AddFlowGateOptions(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidateOptions<FlowGateOptions>, FlowGateOptionsValidator>();
        services.AddSingleton<IValidateOptions<KeeneticOptions>, KeeneticOptionsValidator>();
        services.AddSingleton<IValidateOptions<XrayOptions>, XrayOptionsValidator>();
        services.AddSingleton<IValidateOptions<GitHubOptions>, GitHubOptionsValidator>();
        services.AddSingleton<IValidateOptions<SubscriptionOptions>, SubscriptionOptionsValidator>();
        services.AddSingleton<IValidateOptions<ConnectionOptions>, ConnectionOptionsValidator>();
        services.AddSingleton<IValidateOptions<ConnectionOptions>, ConnectionOptionsRangeValidator>();
        services.AddSingleton<IValidateOptions<DomainSourceOptions>, DomainSourceOptionsValidator>();

        services.AddOptionsWithValidateOnStart<FlowGateOptions>()
            .Bind(configuration.GetSection(FlowGateOptions.SectionName));

        services.AddOptionsWithValidateOnStart<KeeneticOptions>()
            .Bind(configuration.GetSection(KeeneticOptions.SectionName));

        services.AddOptionsWithValidateOnStart<XrayOptions>()
            .Bind(configuration.GetSection(XrayOptions.SectionName));

        services.AddOptionsWithValidateOnStart<GitHubOptions>()
            .Bind(configuration.GetSection(GitHubOptions.SectionName));

        services.AddOptionsWithValidateOnStart<SubscriptionOptions>()
            .Bind(configuration.GetSection(SubscriptionOptions.SectionName));

        services.AddOptionsWithValidateOnStart<ConnectionOptions>()
            .Bind(configuration.GetSection(ConnectionOptions.SectionName));

        services.AddOptionsWithValidateOnStart<DomainSourceOptions>()
            .Bind(configuration.GetSection(DomainSourceOptions.SectionName));

        return services;
    }
}
