using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Jobs;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.WebApi.Endpoints.Jobs;

/// <summary>
///     Фоновые задачи: прогресс долгих операций
/// </summary>
public static class JobEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/jobs</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapJobEndpoints(this IEndpointRouteBuilder routes)
    {
        var jobs = routes.MapGroup("/jobs").WithTags("Jobs");

        jobs.MapGet("/", List)
            .WithName("GetJobs")
            .WithSummary("Выполняющиеся и последние завершённые задачи");

        jobs.MapGet("/{id:guid}", Get)
            .WithName("GetJob")
            .WithSummary("Задача и её шаги")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        return routes;
    }

    private static Ok<IReadOnlyList<JobDto>> List(IJobService jobs) => TypedResults.Ok(jobs.List());

    private static Ok<JobDto> Get(IJobService jobs, Guid id) => TypedResults.Ok(jobs.Get(id));
}
