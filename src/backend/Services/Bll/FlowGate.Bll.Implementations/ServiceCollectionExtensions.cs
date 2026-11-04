using System.Net;

using Microsoft.Extensions.DependencyInjection;

using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Implementations.Subscriptions;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Jobs;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Bll.Interfaces.Subscriptions;
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

        services.AddSingleton<IServerImportParser, ServerImportParser>();
        services.AddSingleton<IOutboundValidator, OutboundValidator>();
        services.AddSingleton<IServerService, ServerService>();

        services.AddSingleton<ISubscriptionFetcher, SubscriptionFetcher>();
        services.AddSingleton<ISubscriptionService, SubscriptionService>();
        services.AddSubscriptionHttpClient();

        return services;
    }

    /// <summary>
    ///     Регистрирует HTTP-клиент подписок: перенаправления выключены (их обрабатывает <see cref="SubscriptionFetcher"/>),
    ///     распаковка gzip/deflate/br включена, таймаут задаётся на запрос
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Построитель HTTP-клиента, например для подмены обработчика в тестах</returns>
    public static IHttpClientBuilder AddSubscriptionHttpClient(this IServiceCollection services) =>
        services.AddHttpClient(SubscriptionFetcher.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });
}
