using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Microsoft.AspNetCore.Http.HttpResults;

using FlowGate.Shared.Changes;

namespace FlowGate.WebApi.Endpoints.Events;

/// <summary>
///     Поток изменений для UI (Server-Sent Events): событие <c>change</c> с темой в данных, раз в
///     <see cref="HeartbeatInterval"/> — <c>ping</c>, чтобы прокси не закрыл простаивающее соединение
/// </summary>
public static class EventEndpoints
{
    /// <summary>
    ///     Тип события «данные темы изменились»; в данных — тема
    /// </summary>
    public const string ChangeEvent = "change";

    /// <summary>
    ///     Тип служебного события: первое после подключения и затем при простое
    /// </summary>
    public const string PingEvent = "ping";

    /// <summary>
    ///     Через сколько простоя отправляется <see cref="PingEvent"/>
    /// </summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(25);

    private enum WaitResult
    {
        Ready,
        Idle,
        Closed,
    }

    /// <summary>
    ///     Регистрирует маршрут <c>/events</c>
    /// </summary>
    /// <param name="routes">Группа маршрутов API</param>
    /// <returns>Та же группа маршрутов</returns>
    public static IEndpointRouteBuilder MapEventEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/events", Stream)
            .WithTags("Events")
            .WithName("GetEvents")
            .WithSummary("Поток изменений (Server-Sent Events): change с темой в данных и ping при простое");

        return routes;
    }

    private static ServerSentEventsResult<string> Stream(
        IChangeFeed changes,
        TimeProvider timeProvider,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        context.Response.Headers["X-Accel-Buffering"] = "no";
        return TypedResults.ServerSentEvents(ReadAsync(changes, timeProvider, cancellationToken));
    }

    private static async IAsyncEnumerable<SseItem<string>> ReadAsync(
        IChangeFeed changes,
        TimeProvider timeProvider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var subscription = changes.Subscribe();
        yield return new SseItem<string>(string.Empty, PingEvent);

        while (true)
        {
            var result = await WaitAsync(subscription.Topics, timeProvider, cancellationToken);
            if (result == WaitResult.Closed)
            {
                yield break;
            }

            if (result == WaitResult.Idle)
            {
                yield return new SseItem<string>(string.Empty, PingEvent);
                continue;
            }

            var topics = new List<string>();
            while (subscription.Topics.TryRead(out var topic))
            {
                if (!topics.Contains(topic))
                {
                    topics.Add(topic);
                }
            }

            foreach (var topic in topics)
            {
                yield return new SseItem<string>(topic, ChangeEvent);
            }
        }
    }

    private static async Task<WaitResult> WaitAsync(
        ChannelReader<string> topics,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        using var idle = new CancellationTokenSource(HeartbeatInterval, timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, idle.Token);
        try
        {
            return await topics.WaitToReadAsync(linked.Token) ? WaitResult.Ready : WaitResult.Closed;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return WaitResult.Idle;
        }
        catch (OperationCanceledException)
        {
            return WaitResult.Closed;
        }
    }
}
