using System.Reflection;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using FlowGate.Shared.Options;

namespace FlowGate.Shared.GitHub;

/// <summary>
///     Регистрация клиента релизов GitHub
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly string UserAgent =
        $"FlowGate/{typeof(ServiceCollectionExtensions).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "dev"}";

    /// <summary>
    ///     Регистрирует <see cref="IGitHubReleasesClient"/> как типизированный HTTP-клиент с настройками из <see cref="GitHubOptions"/>
    /// </summary>
    /// <param name="services">Коллекция сервисов</param>
    /// <returns>Построитель HTTP-клиента, например для подмены обработчика в тестах</returns>
    public static IHttpClientBuilder AddGitHubReleasesClient(this IServiceCollection services) =>
        services.AddHttpClient<IGitHubReleasesClient, GitHubReleasesClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<GitHubOptions>>().Value;
            var baseUrl = options.ApiBaseUrl.EndsWith('/') ? options.ApiBaseUrl : options.ApiBaseUrl + "/";

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
            client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        });
}
