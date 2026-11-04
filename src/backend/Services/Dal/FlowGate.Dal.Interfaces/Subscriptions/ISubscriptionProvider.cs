using FlowGate.Contracts.Subscriptions;

namespace FlowGate.Dal.Interfaces.Subscriptions;

/// <summary>
///     Хранение подписок (<c>subscriptions.json</c>)
/// </summary>
public interface ISubscriptionProvider
{
    /// <summary>
    ///     Возвращает все подписки
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подписки в порядке добавления</returns>
    Task<IReadOnlyList<Subscription>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает подписку
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подписка или null</returns>
    Task<Subscription?> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет подписку
    /// </summary>
    /// <param name="subscription">Подписка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken);

    /// <summary>
    ///     Изменяет подписку под блокировкой хранилища
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="update">Функция, возвращающая новую подписку по текущей</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённая подписка или null, если её нет</returns>
    Task<Subscription?> UpdateAsync(string id, Func<Subscription, Subscription> update, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет подписку
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если подписка была</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken);
}
