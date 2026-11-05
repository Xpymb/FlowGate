using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Subscriptions;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.Subscriptions;

/// <summary>
///     Подписки для API и планировщика
/// </summary>
public interface ISubscriptionService
{
    /// <summary>
    ///     Возвращает все подписки
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подписки</returns>
    Task<IReadOnlyList<SubscriptionDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает подписку
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подписка</returns>
    /// <exception cref="EntityNotFoundException">Если подписки нет</exception>
    Task<SubscriptionDto> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Создаёт подписку и сразу запускает её обновление
    /// </summary>
    /// <param name="request">Параметры подписки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Подписка с идентификатором задачи обновления</returns>
    /// <exception cref="RequestValidationException">Если параметры некорректны</exception>
    Task<SubscriptionDto> CreateAsync(SubscriptionSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Изменяет подписку; смена адреса сбрасывает сведения прежнего провайдера
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="request">Параметры подписки</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённая подписка</returns>
    /// <exception cref="EntityNotFoundException">Если подписки нет</exception>
    /// <exception cref="RequestValidationException">Если параметры некорректны</exception>
    Task<SubscriptionDto> UpdateAsync(string id, SubscriptionSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет подписку вместе с её серверами
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если подписки нет</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает фоновое обновление подписки
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="via">Как загружать: по настройкам, только через прокси обновлений или напрямую</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Запущенная задача</returns>
    /// <exception cref="EntityNotFoundException">Если подписки нет</exception>
    /// <exception cref="ConflictException">Если эта подписка уже обновляется</exception>
    Task<JobDto> RefreshAsync(string id, UpdateVia via, CancellationToken cancellationToken);

    /// <summary>
    ///     Обновляет по очереди все подписки, которым пора обновиться. Ошибки отдельных подписок записываются в них, а не бросаются
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько подписок обновлялось</returns>
    Task<int> RefreshDueAsync(CancellationToken cancellationToken);
}
