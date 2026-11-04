using System.Text.Json.Serialization;

namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Подписка на серверы провайдера
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя, заданное пользователем, или null</param>
/// <param name="Url">Адрес подписки; содержит токен доступа, в логи не пишется</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
/// <param name="UpdateIntervalHours">Интервал, заданный пользователем, или null</param>
/// <param name="UserAgent">Свой User-Agent или null — <c>FlowGate/&lt;версия&gt;</c></param>
/// <param name="SendHwid">Отправлять <c>x-hwid</c></param>
/// <param name="Hwid">Постоянный идентификатор устройства для этой подписки</param>
/// <param name="SendDeviceInfo">Сообщать провайдеру модель и версию роутера</param>
/// <param name="Provider">Сведения от провайдера или null</param>
/// <param name="LastRefresh">Результат последнего обновления или null</param>
/// <param name="CreatedAt">Когда добавлена</param>
public sealed record Subscription(
    string Id,
    string? Name,
    string Url,
    bool AutoUpdate,
    int? UpdateIntervalHours,
    string? UserAgent,
    bool SendHwid,
    string Hwid,
    bool SendDeviceInfo,
    ProviderMetadata? Provider,
    SubscriptionRefreshResult? LastRefresh,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    ///     Имя для показа: пользователя, иначе провайдера, иначе хост подписки
    /// </summary>
    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Name) ? Name
        : !string.IsNullOrWhiteSpace(Provider?.Title) ? Provider.Title
        : Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.Host
        : Id;

    /// <summary>
    ///     Действующий интервал обновления: пользователя, иначе провайдера, иначе по умолчанию
    /// </summary>
    /// <param name="defaultInterval">Интервал по умолчанию</param>
    /// <returns>Интервал</returns>
    public TimeSpan EffectiveInterval(TimeSpan defaultInterval) =>
        UpdateIntervalHours is { } user ? TimeSpan.FromHours(user)
        : Provider?.UpdateIntervalHours is { } provider ? TimeSpan.FromHours(provider)
        : defaultInterval;

    /// <summary>
    ///     Когда подписке пора обновиться; после ошибки — через <paramref name="retryAfterFailure"/>, если это раньше интервала
    /// </summary>
    /// <param name="defaultInterval">Интервал по умолчанию</param>
    /// <param name="retryAfterFailure">Пауза перед повтором после ошибки</param>
    /// <returns>Время следующего обновления или null, если автообновление выключено</returns>
    public DateTimeOffset? NextRefreshAt(TimeSpan defaultInterval, TimeSpan retryAfterFailure)
    {
        if (!AutoUpdate)
        {
            return null;
        }

        if (LastRefresh is null)
        {
            return CreatedAt;
        }

        var interval = EffectiveInterval(defaultInterval);
        var wait = LastRefresh.Status == SubscriptionRefreshStatus.Failed && retryAfterFailure < interval
            ? retryAfterFailure
            : interval;
        return LastRefresh.At + wait;
    }
}
