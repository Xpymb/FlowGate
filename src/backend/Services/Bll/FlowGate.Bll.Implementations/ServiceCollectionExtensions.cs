using System.Net;

using Microsoft.Extensions.DependencyInjection;

using FlowGate.Bll.Implementations.Connections;
using FlowGate.Bll.Implementations.DnsRoutes;
using FlowGate.Bll.Implementations.DomainSources;
using FlowGate.Bll.Implementations.DomainSources.Readers;
using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Implementations.Maintenance;
using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Implementations.Subscriptions;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Connections;
using FlowGate.Bll.Interfaces.DnsRoutes;
using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Bll.Interfaces.Jobs;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Maintenance;
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
        services.AddSingleton<IXrayConfigSource, ConnectionXrayConfigSource>();
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

        services.AddSingleton<IKeeneticInterfaceService, KeeneticInterfaceService>();
        services.AddSingleton<IWireguardKeyGenerator, XrayWireguardKeyGenerator>();
        services.AddSingleton<IConnectionService, ConnectionService>();

        services.AddSingleton<IListSourceReader, GeositeDatReader>();
        services.AddSingleton<IListSourceReader, GeoipDatReader>();
        services.AddSingleton<IListSourceReader, GitHubRepositoryReader>();
        services.AddSingleton<IListSourceReader, LocalSourceReader>();
        services.AddSingleton<IDomainListReader, DomainListReader>();
        services.AddSingleton<IDomainSourceDownloader, DomainSourceDownloader>();
        services.AddSingleton<IDomainSourceService, DomainSourceService>();
        services.AddDomainSourceHttpClient();

        services.AddSingleton<IKeeneticDnsService, KeeneticDnsService>();
        services.AddSingleton<IDnsRouteService, DnsRouteService>();

        services.AddSingleton<ICleanupService, CleanupService>();
        services.AddSingleton<ISystemService, SystemService>();

        return services;
    }

    /// <summary>
    ///     Регистрирует HTTP-клиент загрузки источников по прямой ссылке: перенаправления и распаковка включены, таймаут
    ///     задаётся на запрос
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Построитель HTTP-клиента, например для подмены обработчика в тестах</returns>
    public static IHttpClientBuilder AddDomainSourceHttpClient(this IServiceCollection services) =>
        services.AddHttpClient(DomainSourceDownloader.HttpClientName, client => client.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            });

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
