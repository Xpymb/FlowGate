using System.Reflection;

using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.SharedContracts.Dto.Health;

namespace FlowGate.WebApi.Endpoints.Health;

/// <summary>
///     Проверка живости приложения
/// </summary>
public static class HealthEndpoints
{
    private static readonly string Version =
        typeof(HealthEndpoints).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(HealthEndpoints).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    /// <summary>
    ///     Регистрирует <c>GET /health</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", GetHealth)
            .WithName("GetHealth")
            .WithTags("System")
            .WithSummary("Проверка живости приложения");

        return routes;
    }

    private static Ok<HealthDto> GetHealth() => TypedResults.Ok(new HealthDto("ok", Version));
}
