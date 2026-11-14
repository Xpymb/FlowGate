using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.WebApi.Endpoints.Servers;

/// <summary>
///     Серверы: список, разбор ссылки, проверка и правка outbound, импорт
/// </summary>
public static class ServerEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/servers</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapServerEndpoints(this IEndpointRouteBuilder routes)
    {
        var servers = routes.MapGroup("/servers").WithTags("Servers");

        servers.MapGet("/", GetAllAsync)
            .WithName("GetServers")
            .WithSummary("Все серверы");

        servers.MapGet("/{id}", GetAsync)
            .WithName("GetServer")
            .WithSummary("Сервер с outbound для редактора")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        servers.MapPost("/parse", Parse)
            .WithName("ParseServer")
            .WithSummary("Разобрать ссылку или Xray-JSON в outbound без сохранения")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        servers.MapPost("/validate", ValidateAsync)
            .WithName("ValidateServer")
            .WithSummary("Проверить outbound установленным Xray");

        servers.MapPost("/", CreateAsync)
            .WithName("CreateServer")
            .WithSummary("Создать ручной сервер")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        servers.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateServer")
            .WithSummary("Изменить ручной сервер")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        servers.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteServer")
            .WithSummary("Удалить ручной сервер")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        servers.MapPost("/import", ImportAsync)
            .WithName("ImportServers")
            .WithSummary("Импортировать несколько серверов как ручные")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        servers.MapPost("/preview", Preview)
            .WithName("PreviewServers")
            .WithSummary("Разобрать ссылки или Xray-JSON в список серверов без сохранения")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<ServerDto>>> GetAllAsync(IServerService servers, CancellationToken cancellationToken) =>
        TypedResults.Ok(await servers.GetAllAsync(cancellationToken));

    private static async Task<Ok<ServerDetailsDto>> GetAsync(IServerService servers, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await servers.GetAsync(id, cancellationToken));

    private static Ok<ServerDraftDto> Parse(IServerService servers, ServerParseRequest request) =>
        TypedResults.Ok(servers.Parse(request));

    private static async Task<Ok<ServerValidationDto>> ValidateAsync(
        IServerService servers,
        ServerValidateRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await servers.ValidateAsync(request, cancellationToken));

    private static async Task<Created<ServerDetailsDto>> CreateAsync(
        IServerService servers,
        ServerSaveRequest request,
        CancellationToken cancellationToken)
    {
        var created = await servers.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/servers/{created.Server.Id}", created);
    }

    private static async Task<Ok<ServerDetailsDto>> UpdateAsync(
        IServerService servers,
        string id,
        ServerSaveRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await servers.UpdateAsync(id, request, cancellationToken));

    private static async Task<NoContent> DeleteAsync(IServerService servers, string id, CancellationToken cancellationToken)
    {
        await servers.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<ServerImportResultDto>> ImportAsync(
        IServerService servers,
        ServerImportRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await servers.ImportAsync(request, cancellationToken));

    private static Ok<ServerPreviewDto> Preview(IServerService servers, ServerImportRequest request) =>
        TypedResults.Ok(servers.Preview(request));
}
