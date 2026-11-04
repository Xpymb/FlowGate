using FlowGate.Contracts.Subscriptions;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.Subscriptions;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.Subscriptions;

/// <summary>
///     Подписки в <c>subscriptions.json</c>
/// </summary>
public sealed class SubscriptionProvider : ISubscriptionProvider
{
    /// <summary>
    ///     Имя файла подписок
    /// </summary>
    public const string FileName = "subscriptions.json";

    private readonly IJsonFileStore<SubscriptionCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public SubscriptionProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.SubscriptionCatalog, () => SubscriptionCatalog.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Subscriptions;

    /// <inheritdoc />
    public async Task<Subscription?> GetAsync(string id, CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Subscriptions.FirstOrDefault(subscription => subscription.Id == id);

    /// <inheritdoc />
    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return _store.UpdateAsync(
            catalog => catalog.Subscriptions.Any(existing => existing.Id == subscription.Id)
                ? throw new InvalidOperationException($"Подписка {subscription.Id} уже существует")
                : new SubscriptionCatalog([.. catalog.Subscriptions, subscription]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Subscription?> UpdateAsync(string id, Func<Subscription, Subscription> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        Subscription? updated = null;
        await _store.UpdateAsync(
            catalog => new SubscriptionCatalog(catalog.Subscriptions
                .Select(existing => existing.Id == id ? updated = update(existing) : existing)
                .ToList()),
            cancellationToken);

        return updated;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Subscriptions.Any(subscription => subscription.Id == id);
                return found ? new SubscriptionCatalog(catalog.Subscriptions.Where(subscription => subscription.Id != id).ToList()) : catalog;
            },
            cancellationToken);

        return found;
    }
}
