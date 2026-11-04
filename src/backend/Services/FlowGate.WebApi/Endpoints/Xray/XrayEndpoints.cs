using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.WebApi.Endpoints.Xray;

/// <summary>
///     Управление Xray: статус, релизы, установка, запуск, логи
/// </summary>
public static class XrayEndpoints
{
    private const string Tag = "Xray";

    /// <summary>
    ///     Регистрирует маршруты <c>/xray</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapXrayEndpoints(this IEndpointRouteBuilder routes)
    {
        var xray = routes.MapGroup("/xray").WithTags(Tag);

        xray.MapGet("/", GetStatusAsync)
            .WithName("GetXrayStatus")
            .WithSummary("Состояние Xray и доступное обновление");

        xray.MapGet("/releases", GetReleasesAsync)
            .WithName("GetXrayReleases")
            .WithSummary("Релизы Xray, доступные для установки")
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        xray.MapPost("/install", InstallAsync)
            .WithName("InstallXray")
            .WithSummary("Запустить установку версии (по умолчанию последней стабильной)")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status503ServiceUnavailable);

        xray.MapPost("/start", StartAsync)
            .WithName("StartXray")
            .WithSummary("Запустить Xray и включить автозапуск")
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        xray.MapPost("/stop", StopAsync)
            .WithName("StopXray")
            .WithSummary("Остановить Xray и выключить автозапуск");

        xray.MapPost("/restart", RestartAsync)
            .WithName("RestartXray")
            .WithSummary("Перезапустить Xray с пересобранной конфигурацией")
            .Produces<ErrorResponse>(StatusCodes.Status500InternalServerError);

        xray.MapGet("/logs", GetLogs)
            .WithName("GetXrayLogs")
            .WithSummary("Последние строки вывода Xray");

        return routes;
    }

    private static async Task<Ok<XrayStatusDto>> GetStatusAsync(IXrayService xray, CancellationToken cancellationToken) =>
        TypedResults.Ok(await xray.GetStatusAsync(cancellationToken));

    private static async Task<Ok<IReadOnlyList<XrayReleaseDto>>> GetReleasesAsync(
        IXrayService xray,
        CancellationToken cancellationToken,
        bool includePrerelease = false,
        bool refresh = false) =>
        TypedResults.Ok(await xray.GetReleasesAsync(includePrerelease, refresh, cancellationToken));

    private static async Task<Accepted<JobDto>> InstallAsync(
        IXrayService xray,
        XrayInstallRequest? request,
        CancellationToken cancellationToken)
    {
        var job = await xray.InstallAsync(request ?? new XrayInstallRequest(null), cancellationToken);
        return TypedResults.Accepted($"/api/jobs/{job.Id}", job);
    }

    private static async Task<Ok<XrayStatusDto>> StartAsync(IXrayService xray, CancellationToken cancellationToken) =>
        TypedResults.Ok(await xray.StartAsync(cancellationToken));

    private static async Task<Ok<XrayStatusDto>> StopAsync(IXrayService xray, CancellationToken cancellationToken) =>
        TypedResults.Ok(await xray.StopAsync(cancellationToken));

    private static async Task<Ok<XrayStatusDto>> RestartAsync(IXrayService xray, CancellationToken cancellationToken) =>
        TypedResults.Ok(await xray.RestartAsync(cancellationToken));

    private static Ok<XrayLogDto> GetLogs(IXrayService xray, int tail = 200) => TypedResults.Ok(xray.GetLogs(tail));
}
