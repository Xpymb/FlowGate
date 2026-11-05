using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Connections;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Connections;

namespace FlowGate.WebApi.Endpoints.Connections;

/// <summary>
///     Соединения: inbound Xray + интерфейс Keenetic + выбранный сервер
/// </summary>
public static class ConnectionEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/connections</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapConnectionEndpoints(this IEndpointRouteBuilder routes)
    {
        var connections = routes.MapGroup("/connections").WithTags("Connections");

        connections.MapGet("/", GetAllAsync)
            .WithName("GetConnections")
            .WithSummary("Все соединения с состоянием интерфейсов Keenetic");

        connections.MapGet("/{id}", GetAsync)
            .WithName("GetConnection")
            .WithSummary("Соединение с состоянием интерфейса")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        connections.MapPost("/", CreateAsync)
            .WithName("CreateConnection")
            .WithSummary("Создать соединение: inbound Xray и интерфейс Keenetic")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict)
            .Produces<ErrorResponse>(StatusCodes.Status502BadGateway);

        connections.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateConnection")
            .WithSummary("Изменить имя и сервер соединения")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        connections.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteConnection")
            .WithSummary("Удалить соединение вместе с интерфейсом Keenetic")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<ConnectionDto>>> GetAllAsync(IConnectionService connections, CancellationToken cancellationToken) =>
        TypedResults.Ok(await connections.GetAllAsync(cancellationToken));

    private static async Task<Ok<ConnectionDto>> GetAsync(IConnectionService connections, string id, CancellationToken cancellationToken) =>
        TypedResults.Ok(await connections.GetAsync(id, cancellationToken));

    private static async Task<Created<ConnectionDto>> CreateAsync(
        IConnectionService connections,
        ConnectionCreateRequest request,
        CancellationToken cancellationToken)
    {
        var created = await connections.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/connections/{created.Id}", created);
    }

    private static async Task<Ok<ConnectionDto>> UpdateAsync(
        IConnectionService connections,
        string id,
        ConnectionUpdateRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await connections.UpdateAsync(id, request, cancellationToken));

    private static async Task<NoContent> DeleteAsync(IConnectionService connections, string id, CancellationToken cancellationToken)
    {
        await connections.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }
}
