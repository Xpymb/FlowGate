using FlowGate.Contracts.Subscriptions;
using FlowGate.SharedContracts.Dto.Subscriptions;

namespace FlowGate.Bll.Implementations.Subscriptions;

/// <summary>
///     Маппинг подписок в DTO
/// </summary>
public static class SubscriptionMapper
{
    /// <summary>
    ///     Преобразует подписку в DTO
    /// </summary>
    /// <param name="subscription">Подписка</param>
    /// <param name="serverCount">Число серверов подписки</param>
    /// <param name="defaultInterval">Интервал обновления по умолчанию</param>
    /// <param name="retryAfterFailure">Пауза перед повтором после ошибки</param>
    /// <param name="refreshJobId">Задача обновления, запущенная запросом, или null</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если subscription равен null</exception>
    public static SubscriptionDto ToDto(
        this Subscription subscription,
        int serverCount,
        TimeSpan defaultInterval,
        TimeSpan retryAfterFailure,
        Guid? refreshJobId = null)
    {
        ArgumentNullException.ThrowIfNull(subscription);

        return new SubscriptionDto(
            subscription.Id,
            subscription.DisplayName,
            subscription.Name,
            subscription.Url,
            subscription.AutoUpdate,
            subscription.UpdateIntervalHours,
            subscription.EffectiveInterval(defaultInterval).TotalHours,
            subscription.UserAgent,
            subscription.SendHwid,
            subscription.Hwid,
            subscription.SendDeviceInfo,
            serverCount,
            subscription.Provider?.ToDto(),
            subscription.LastRefresh?.ToDto(),
            subscription.NextRefreshAt(defaultInterval, retryAfterFailure),
            subscription.CreatedAt,
            refreshJobId);
    }

    /// <summary>
    ///     Преобразует метаданные провайдера в DTO
    /// </summary>
    /// <param name="metadata">Метаданные</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если metadata равен null</exception>
    public static ProviderMetadataDto ToDto(this ProviderMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return new ProviderMetadataDto(
            metadata.Title,
            metadata.UploadBytes,
            metadata.DownloadBytes,
            metadata.TotalBytes,
            metadata.ExpireAt,
            metadata.UpdateIntervalHours,
            metadata.Announce,
            metadata.AnnounceUrl,
            metadata.SupportUrl,
            metadata.WebPageUrl);
    }

    /// <summary>
    ///     Преобразует результат обновления в DTO
    /// </summary>
    /// <param name="result">Результат</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если result равен null</exception>
    public static SubscriptionRefreshDto ToDto(this SubscriptionRefreshResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new SubscriptionRefreshDto(
            result.At,
            result.Status.ToString(),
            result.Error,
            result.Received,
            result.Accepted,
            result.Rejected,
            result.Added,
            result.Updated,
            result.Removed,
            result.Retained,
            result.Warnings);
    }
}
