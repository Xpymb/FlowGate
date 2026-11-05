using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using FlowGate.Shared.Options;

namespace FlowGate.Shared.Keenetic;

/// <summary>
///     Регистрация клиента RCI Keenetic
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Регистрирует <see cref="IKeeneticRciClient"/> как типизированный HTTP-клиент с настройками из <see cref="KeeneticOptions"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Построитель HTTP-клиента, например для подмены обработчика в тестах</returns>
    public static IHttpClientBuilder AddKeeneticRciClient(this IServiceCollection services) =>
        services.AddHttpClient<IKeeneticRciClient, KeeneticRciClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KeeneticOptions>>().Value;
            var baseUrl = options.RciBaseUrl.EndsWith('/') ? options.RciBaseUrl : options.RciBaseUrl + "/";

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
        });
}
