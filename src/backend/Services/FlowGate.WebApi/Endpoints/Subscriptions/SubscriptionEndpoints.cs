using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Bll.Interfaces.Subscriptions;
using FlowGate.Shared.WebApi.Errors;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Subscriptions;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.WebApi.Endpoints.Subscriptions;

/// <summary>
///     Подписки: CRUD и запуск обновления
/// </summary>
public static class SubscriptionEndpoints
{
    /// <summary>
    ///     Регистрирует маршруты <c>/subscriptions</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapSubscriptionEndpoints(this IEndpointRouteBuilder routes)
    {
        var subscriptions = routes.MapGroup("/subscriptions").WithTags("Subscriptions");

        subscriptions.MapGet("/", GetAllAsync)
            .WithName("GetSubscriptions")
            .WithSummary("Все подписки");

        subscriptions.MapGet("/{id}", GetAsync)
            .WithName("GetSubscription")
            .WithSummary("Подписка")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        subscriptions.MapPost("/", CreateAsync)
            .WithName("CreateSubscription")
            .WithSummary("Добавить подписку и запустить её обновление")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest);

        subscriptions.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateSubscription")
            .WithSummary("Изменить подписку")
            .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        subscriptions.MapDelete("/{id}", DeleteAsync)
            .WithName("DeleteSubscription")
            .WithSummary("Удалить подписку вместе с её серверами")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        subscriptions.MapPost("/{id}/refresh", RefreshAsync)
            .WithName("RefreshSubscription")
            .WithSummary("Запустить обновление подписки; via — Auto (по настройкам), Proxy или Direct")
            .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
            .Produces<ErrorResponse>(StatusCodes.Status409Conflict);

        return routes;
    }

    private static async Task<Ok<IReadOnlyList<SubscriptionDto>>> GetAllAsync(
        ISubscriptionService subscriptions,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await subscriptions.GetAllAsync(cancellationToken));

    private static async Task<Ok<SubscriptionDto>> GetAsync(
        ISubscriptionService subscriptions,
        string id,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await subscriptions.GetAsync(id, cancellationToken));

    private static async Task<Created<SubscriptionDto>> CreateAsync(
        ISubscriptionService subscriptions,
        SubscriptionSaveRequest request,
        CancellationToken cancellationToken)
    {
        var created = await subscriptions.CreateAsync(request, cancellationToken);
        return TypedResults.Created($"/api/subscriptions/{created.Id}", created);
    }

    private static async Task<Ok<SubscriptionDto>> UpdateAsync(
        ISubscriptionService subscriptions,
        string id,
        SubscriptionSaveRequest request,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await subscriptions.UpdateAsync(id, request, cancellationToken));

    private static async Task<NoContent> DeleteAsync(
        ISubscriptionService subscriptions,
        string id,
        CancellationToken cancellationToken)
    {
        await subscriptions.DeleteAsync(id, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Accepted<JobDto>> RefreshAsync(
        ISubscriptionService subscriptions,
        string id,
        CancellationToken cancellationToken,
        UpdateVia via = UpdateVia.Auto)
    {
        var job = await subscriptions.RefreshAsync(id, via, cancellationToken);
        return TypedResults.Accepted($"/api/jobs/{job.Id}", job);
    }
}
