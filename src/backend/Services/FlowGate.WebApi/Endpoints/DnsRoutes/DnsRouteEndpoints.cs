using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.DnsRoutes;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.DnsRoutes;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.WebApi.Endpoints.DnsRoutes;

/// <summary>
///     DNS-маршруты: список источника → соединение. Изменения применяются в Keenetic фоновой задачей
/// </summary>
public static class DnsRouteEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/dns-routes</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapDnsRouteEndpoints(this IEndpointRouteBuilder routes)
    {
        var dnsRoutes = routes.MapGroup("/dns-routes").WithTags("DnsRoutes");

        dnsRoutes.MapGet("/", GetAllAsync).WithName("GetDnsRoutes").WithSummary("Все DNS-маршруты");

        dnsRoutes.MapGet("/{id}", GetAsync)
            .WithName("GetDnsRoute")
            .WithSummary("DNS-маршрут")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        dnsRoutes.MapPost("/", CreateAsync)
            .WithName("CreateDnsRoute")
            .WithSummary("Направить список в соединение")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        dnsRoutes.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateDnsRoute")
            .WithSummary("Сменить соединение или фильтр")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        dnsRoutes.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteDnsRoute")
            .WithSummary("Удалить маршрут вместе с группами в Keenetic")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        dnsRoutes.MapPost("/{id}/enable", EnableAsync)
            .WithName("EnableDnsRoute")
            .WithSummary("Включить правило в Keenetic")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        dnsRoutes.MapPost("/{id}/disable", DisableAsync)
            .WithName("DisableDnsRoute")
            .WithSummary("Выключить правило в Keenetic")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        dnsRoutes.MapPost("/apply", Apply)
            .WithName("ApplyDnsRoutes")
            .WithSummary("Применить все маршруты (фоновая задача)");

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<DnsRouteDto>>> GetAllAsync(IDnsRouteService routes, CancellationToken cancellationToken) =>
        TypedResults.Ok(await routes.GetAllAsync(cancellationToken));

    private static async Task<Ok<DnsRouteDto>> GetAsync(IDnsRouteService routes, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await routes.GetAsync(id, cancellationToken));

    private static async Task<Created<DnsRouteDto>> CreateAsync(IDnsRouteService routes, DnsRouteCreateRequest request, CancellationToken cancellationToken)
    {
        var created = await routes.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/dns-routes/{created.Id}", created);
    }

    private static async Task<Ok<DnsRouteDto>> UpdateAsync(
        IDnsRouteService routes,
        string id,
        DnsRouteUpdateRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await routes.UpdateAsync(id, request, cancellationToken));

    private static async Task<NoContent> DeleteAsync(IDnsRouteService routes, string id, CancellationToken cancellationToken)
    {
        await routes.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<DnsRouteDto>> EnableAsync(IDnsRouteService routes, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await routes.SetDisabledAsync(id, disabled: false, cancellationToken));

    private static async Task<Ok<DnsRouteDto>> DisableAsync(IDnsRouteService routes, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await routes.SetDisabledAsync(id, disabled: true, cancellationToken));

    private static Accepted<JobDto> Apply(IDnsRouteService routes)
    {
        var job = routes.RequestApply();
        return TypedResults.Accepted($"/api/jobs/{job.Id}", job);
    }
}
