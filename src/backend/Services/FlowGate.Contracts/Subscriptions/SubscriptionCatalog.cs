namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Все подписки (содержимое <c>subscriptions.json</c>)
/// </summary>
/// <param name="Subscriptions">Подписки</param>
public sealed record SubscriptionCatalog(IReadOnlyList<Subscription> Subscriptions)
{
    /// <summary>
    ///     Пустой каталог
    /// </summary>
    public static SubscriptionCatalog Empty { get; } = new([]);
}
