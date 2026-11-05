using FlowGate.Contracts.Servers;

namespace FlowGate.Dal.Interfaces.Servers;

/// <summary>
///     Хранение серверов (<c>servers.json</c>). Возвращаются копии: outbound изменяемый
/// </summary>
public interface IServerProvider
{
    /// <summary>
    ///     Возвращает все серверы
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Серверы в порядке хранения</returns>
    Task<IReadOnlyList<Server>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает сервер
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сервер или null</returns>
    Task<Server?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет серверы в конец списка
    /// </summary>
    /// <param name="servers">Новые серверы</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="InvalidOperationException">Если идентификатор уже занят</exception>
    Task AddAsync(IReadOnlyList<Server> servers, CancellationToken cancellationToken);

    /// <summary>
    ///     Заменяет сервер с тем же идентификатором
    /// </summary>
    /// <param name="server">Сервер</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если сервер найден и заменён</returns>
    Task<bool> UpdateAsync(Server server, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет сервер
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если сервер был</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Атомарно заменяет все серверы подписки новым набором
    /// </summary>
    /// <param name="subscriptionId">Подписка</param>
    /// <param name="servers">Новый набор серверов этой подписки (пустой — удалить все)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task ReplaceSubscriptionServersAsync(string subscriptionId, IReadOnlyList<Server> servers, CancellationToken cancellationToken);
}
