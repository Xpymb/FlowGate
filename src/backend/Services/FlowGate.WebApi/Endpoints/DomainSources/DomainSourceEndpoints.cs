using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.DomainSources;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.WebApi.Endpoints.DomainSources;

/// <summary>
///     Источники списков: GitHub-репозиторий v2fly, geosite.dat, geoip.dat, локальный
/// </summary>
public static class DomainSourceEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/domain-sources</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapDomainSourceEndpoints(this IEndpointRouteBuilder routes)
    {
        var sources = routes.MapGroup("/domain-sources").WithTags("DomainSources");

        sources.MapGet("/", GetAllAsync).WithName("GetDomainSources").WithSummary("Все источники списков");

        sources.MapGet("/{id}", GetAsync)
            .WithName("GetDomainSource")
            .WithSummary("Источник")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapPost("/", CreateAsync)
            .WithName("CreateDomainSource")
            .WithSummary("Добавить источник; загрузка начнётся сразу")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        sources.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateDomainSource")
            .WithSummary("Изменить источник")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteDomainSource")
            .WithSummary("Удалить источник")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        sources.MapPost("/{id}/refresh", RefreshAsync)
            .WithName("RefreshDomainSource")
            .WithSummary("Обновить источник (фоновая задача)")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        sources.MapGet("/{id}/lists", GetListsAsync)
            .WithName("GetDomainLists")
            .WithSummary("Каталог списков источника")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapGet("/{id}/lists/{name}", PreviewAsync)
            .WithName("PreviewDomainList")
            .WithSummary("Что уйдёт в Keenetic для списка с фильтром")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapGet("/{id}/local-lists", GetLocalListsAsync)
            .WithName("GetLocalLists")
            .WithSummary("Тексты списков локального источника")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapPut("/{id}/local-lists/{name}", SaveLocalListAsync)
            .WithName("SaveLocalList")
            .WithSummary("Создать или заменить список локального источника")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        sources.MapDelete("/{id}/local-lists/{name}", DeleteLocalListAsync)
            .WithName("DeleteLocalList")
            .WithSummary("Удалить список локального источника")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<DomainSourceDto>>> GetAllAsync(IDomainSourceService sources, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.GetAllAsync(cancellationToken));

    private static async Task<Ok<DomainSourceDto>> GetAsync(IDomainSourceService sources, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.GetAsync(id, cancellationToken));

    private static async Task<Created<DomainSourceDto>> CreateAsync(
        IDomainSourceService sources,
        DomainSourceSaveRequest request,
        CancellationToken cancellationToken)
    {
        var created = await sources.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/domain-sources/{created.Id}", created);
    }

    private static async Task<Ok<DomainSourceDto>> UpdateAsync(
        IDomainSourceService sources,
        string id,
        DomainSourceSaveRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.UpdateAsync(id, request, cancellationToken));

    private static async Task<NoContent> DeleteAsync(IDomainSourceService sources, string id, CancellationToken cancellationToken)
    {
        await sources.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted<JobDto>> RefreshAsync(
        IDomainSourceService sources,
        string id,
        CancellationToken cancellationToken,
        UpdateVia via = UpdateVia.Auto)
    {
        var job = await sources.RefreshAsync(id, via, cancellationToken);
        return TypedResults.Accepted($"/api/jobs/{job.Id}", job);
    }

    private static async Task<Ok<IReadOnlyList<DomainListSummaryDto>>> GetListsAsync(
        IDomainSourceService sources,
        string id,
        string? query,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.GetListsAsync(id, query, cancellationToken));

    private static async Task<Ok<DomainListPreviewDto>> PreviewAsync(
        IDomainSourceService sources,
        string id,
        string name,
        string? attrs,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.PreviewAsync(id, name, attrs, cancellationToken));

    private static async Task<Ok<IReadOnlyList<LocalListDto>>> GetLocalListsAsync(IDomainSourceService sources, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.GetLocalListsAsync(id, cancellationToken));

    private static async Task<Ok<LocalListDto>> SaveLocalListAsync(
        IDomainSourceService sources,
        string id,
        string name,
        LocalListSaveRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await sources.SaveLocalListAsync(id, name, request, cancellationToken));

    private static async Task<NoContent> DeleteLocalListAsync(IDomainSourceService sources, string id, string name, CancellationToken cancellationToken)
    {
        await sources.DeleteLocalListAsync(id, name, cancellationToken);
        return TypedResults.NoContent();
    }
}
