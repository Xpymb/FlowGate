using FlowGate.Contracts.DnsRoutes;

namespace FlowGate.Dal.Interfaces.DnsRoutes;

/// <summary>
///     Хранение DNS-маршрутов (<c>dns-routes.json</c>)
/// </summary>
public interface IDnsRouteProvider
{
    /// <summary>
    ///     Возвращает все маршруты
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Маршруты в порядке создания</returns>
    Task<IReadOnlyList<DnsRoute>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает маршрут
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Маршрут или null</returns>
    Task<DnsRoute?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет маршрут
    /// </summary>
    /// <param name="route">Маршрут</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="InvalidOperationException">Если идентификатор уже занят</exception>
    Task AddAsync(DnsRoute route, CancellationToken cancellationToken);

    /// <summary>
    ///     Атомарно изменяет маршрут
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="update">Изменение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённый маршрут или null, если его нет</returns>
    Task<DnsRoute?> UpdateAsync(string id, Func<DnsRoute, DnsRoute> update, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет маршрут
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если маршрут был</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
