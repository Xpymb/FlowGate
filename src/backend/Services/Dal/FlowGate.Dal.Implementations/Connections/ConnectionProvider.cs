using FlowGate.Contracts.Connections;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.Connections;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.Connections;

/// <summary>
///     Соединения в <c>connections.json</c>. Файл содержит ключи WireGuard, права 600 даёт <see cref="IJsonFileStore{T}"/>
/// </summary>
public sealed class ConnectionProvider : IConnectionProvider
{
    /// <summary>
    ///     Имя файла соединений
    /// </summary>
    public const string FileName = "connections.json";

    private readonly IJsonFileStore<ConnectionCatalog> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public ConnectionProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.ConnectionCatalog, () => ConnectionCatalog.Empty);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Connections;

    /// <inheritdoc />
    public async Task<Connection?> GetAsync(string id, CancellationToken cancellationToken) =>
        (await _store.ReadAsync(cancellationToken)).Connections.FirstOrDefault(connection => connection.Id == id);

    /// <inheritdoc />
    public Task AddAsync(Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        return _store.UpdateAsync(
            catalog => catalog.Connections.Any(existing => existing.Id == connection.Id)
                ? throw new InvalidOperationException($"Соединение {connection.Id} уже существует")
                : new ConnectionCatalog([.. catalog.Connections, connection]),
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(Connection connection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var found = false;
        await _store.UpdateAsync(
            catalog =>
            {
                found = catalog.Connections.Any(existing => existing.Id == connection.Id);
                return found
                    ? new ConnectionCatalog(catalog.Connections.Select(existing => existing.Id == connection.Id ? connection : existing).ToList())
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
                found = catalog.Connections.Any(connection => connection.Id == id);
                return found ? new ConnectionCatalog(catalog.Connections.Where(connection => connection.Id != id).ToList()) : catalog;
            },
            cancellationToken);

        return found;
    }
}
