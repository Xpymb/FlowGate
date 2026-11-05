using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Keenetic;

namespace FlowGate.WebApi.Endpoints.Keenetic;

/// <summary>
///     Сведения о роутере Keenetic
/// </summary>
public static class KeeneticEndpoints
{
    /// <summary>
    ///     Регистрирует <c>GET /keenetic/system</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapKeeneticEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/keenetic/system", GetSystemInfoAsync)
            .WithName("GetKeeneticSystemInfo")
            .WithTags("Keenetic")
            .WithSummary("Модель, прошивка и совместимость роутера с FlowGate")
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        return routes;
    }

    private static async Task<Ok<KeeneticSystemInfoDto>> GetSystemInfoAsync(
        IKeeneticSystemService service,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await service.GetSystemInfoAsync(cancellationToken));
}
