using FlowGate.Contracts.Subscriptions;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Subscriptions;

/// <summary>
///     Загрузка подписки у провайдера: User-Agent, HWID, сведения об устройстве, лимит размера, перенаправления
/// </summary>
public interface ISubscriptionFetcher
{
    /// <summary>
    ///     Скачивает подписку
    /// </summary>
    /// <param name="subscription">Подписка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Тело и заголовки ответа</returns>
    /// <exception cref="ExternalServiceUnavailableException">Если провайдер недоступен, ответил ошибкой, отклонил HWID или ответ слишком большой</exception>
    Task<SubscriptionDownload> FetchAsync(Subscription subscription, CancellationToken cancellationToken);
}
