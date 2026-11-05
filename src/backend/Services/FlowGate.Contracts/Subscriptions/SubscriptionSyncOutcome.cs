using FlowGate.Contracts.Servers;

namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Результат <see cref="SubscriptionSync.Apply"/>
/// </summary>
/// <param name="Servers">Новый набор серверов подписки</param>
/// <param name="Result">Итог обновления и счётчики</param>
public sealed record SubscriptionSyncOutcome(IReadOnlyList<Server> Servers, SubscriptionRefreshResult Result);
