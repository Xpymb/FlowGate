using FlowGate.Contracts.Servers;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.Servers;

/// <summary>
///     Серверы в <c>servers.json</c>. Наружу и внутрь передаются копии, чтобы изменение outbound вызывающей стороной
///     не меняло кэш хранилища
/// </summary>
public sealed class ServerProvider : IServerProvider
{
    /// <summary>
    ///     Имя файла серверов
    /// </summary>
    public const string FileName = "servers.json";

    private readonly IJsonFileStore<ServerCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public ServerProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.ServerCatalog, () => ServerCatalog.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken)
    {
        var catalog = await _store.ReadAsync(cancellationToken);
        return catalog.Servers.Select(server => server.DeepCopy()).ToList();
    }

    /// <inheritdoc />
    public async Task<Server?> GetAsync(string id, CancellationToken cancellationToken)
    {
        var catalog = await _store.ReadAsync(cancellationToken);
        return catalog.Servers.FirstOrDefault(server => server.Id == id)?.DeepCopy();
    }

    /// <inheritdoc />
    public Task AddAsync(IReadOnlyList<Server> servers, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(servers);

        return _store.UpdateAsync(
            catalog =>
            {
                var ids = catalog.Servers.Select(server => server.Id).ToHashSet(StringComparer.Ordinal);
                var duplicate = servers.FirstOrDefault(server => !ids.Add(server.Id));
                if (duplicate is not null)
                {
                    throw new InvalidOperationException($"Сервер {duplicate.Id} уже существует");
                }

                return new ServerCatalog([.. catalog.Servers, .. servers.Select(server => server.DeepCopy())]);
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Server server, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(server);

        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Servers.Any(existing => existing.Id == server.Id);
                return found
                    ? new ServerCatalog(catalog.Servers.Select(existing => existing.Id == server.Id ? server.DeepCopy() : existing).ToList())
                    : catalog;
            },
            cancellationToken);

        return found;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Servers.Any(server => server.Id == id);
                return found ? new ServerCatalog(catalog.Servers.Where(server => server.Id != id).ToList()) : catalog;
            },
            cancellationToken);

        return found;
    }

    /// <inheritdoc />
    public Task ReplaceSubscriptionServersAsync(string subscriptionId, IReadOnlyList<Server> servers, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(servers);

        return _store.UpdateAsync(
            catalog => new ServerCatalog(
            [
                .. catalog.Servers.Where(server => server.SubscriptionId != subscriptionId),
                .. servers.Select(server => server.DeepCopy()),
            ]),
            cancellationToken);
    }
}
