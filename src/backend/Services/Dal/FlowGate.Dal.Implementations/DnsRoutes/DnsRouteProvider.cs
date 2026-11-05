using FlowGate.Contracts.DnsRoutes;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.DnsRoutes;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.DnsRoutes;

/// <summary>
///     DNS-маршруты в <c>dns-routes.json</c>
/// </summary>
public sealed class DnsRouteProvider : IDnsRouteProvider
{
    /// <summary>
    ///     Имя файла маршрутов
    /// </summary>
    public const string FileName = "dns-routes.json";

    private readonly IJsonFileStore<DnsRouteCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DnsRouteProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public DnsRouteProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.DnsRouteCatalog, () => DnsRouteCatalog.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DnsRoute>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Routes;

    /// <inheritdoc />
    public async Task<DnsRoute?> GetAsync(string id, CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Routes.FirstOrDefault(route => route.Id == id);

    /// <inheritdoc />
    public Task AddAsync(DnsRoute route, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(route);

        return _store.UpdateAsync(
            catalog => catalog.Routes.Any(existing => existing.Id == route.Id)
                ? throw new InvalidOperationException($"Маршрут {route.Id} уже существует")
                : new DnsRouteCatalog([.. catalog.Routes, route]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DnsRoute?> UpdateAsync(string id, Func<DnsRoute, DnsRoute> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        DnsRoute? updated = null;
        await _store.UpdateAsync(
            catalog => new DnsRouteCatalog(catalog.Routes
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
                found = catalog.Routes.Any(route => route.Id == id);
                return found ? new DnsRouteCatalog(catalog.Routes.Where(route => route.Id != id).ToList()) : catalog;
            },
            cancellationToken);

        return found;
    }
}
