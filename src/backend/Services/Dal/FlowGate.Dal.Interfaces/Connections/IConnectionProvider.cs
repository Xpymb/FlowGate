using FlowGate.Contracts.Connections;

namespace FlowGate.Dal.Interfaces.Connections;

/// <summary>
///     Хранение соединений (<c>connections.json</c>)
/// </summary>
public interface IConnectionProvider
{
    /// <summary>
    ///     Возвращает все соединения
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Соединения в порядке создания</returns>
    Task<IReadOnlyList<Connection>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает соединение
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Соединение или null</returns>
    Task<Connection?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет соединение в конец списка
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="InvalidOperationException">Если идентификатор уже занят</exception>
    Task AddAsync(Connection connection, CancellationToken cancellationToken);

    /// <summary>
    ///     Заменяет соединение с тем же идентификатором
    /// </summary>
    /// <param name="connection">Соединение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если соединение найдено и заменено</returns>
    Task<bool> UpdateAsync(Connection connection, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет соединение
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если соединение было</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
