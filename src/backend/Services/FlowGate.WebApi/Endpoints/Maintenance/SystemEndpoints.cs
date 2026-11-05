using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Maintenance;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Maintenance;

namespace FlowGate.WebApi.Endpoints.Maintenance;

/// <summary>
///     Состояние FlowGate и самообновление
/// </summary>
public static class SystemEndpoints
{
    private const string Tag = "System";

    /// <summary>
    ///     Регистрирует маршруты <c>/system</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapSystemEndpoints(this IEndpointRouteBuilder routes)
    {
        var system = routes.MapGroup("/system").WithTags(Tag);

        system.MapGet("/", GetStatusAsync)
            .WithName("GetSystemStatus")
            .WithSummary("Версия FlowGate, доступное обновление, время работы и память");

        system.MapPost("/update", RequestUpgrade)
            .WithName("UpdateFlowGate")
            .WithSummary("Обновить FlowGate через opkg; служба перезапустится")
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<Ok<SystemStatusDto>> GetStatusAsync(ISystemService system, CancellationToken cancellationToken) =>
        TypedResults.Ok(await system.GetStatusAsync(cancellationToken));

    private static Accepted<JobDto> RequestUpgrade(ISystemService system)
    {
        var job = system.RequestUpgrade();
        return TypedResults.Accepted($"/api/jobs/{job.Id}", job);
    }
}
