namespace FlowGate.SharedContracts.Dto.Subscriptions;

/// <summary>
///     Подписка
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="DisplayName">Имя для показа: своё, иначе провайдера, иначе хост</param>
/// <param name="Name">Своё имя или null</param>
/// <param name="Url">Адрес подписки (содержит токен доступа)</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
/// <param name="UpdateIntervalHours">Свой интервал или null</param>
/// <param name="EffectiveIntervalHours">Действующий интервал, часы</param>
/// <param name="UserAgent">Свой User-Agent или null</param>
/// <param name="SendHwid">Отправлять <c>x-hwid</c></param>
/// <param name="Hwid">Постоянный идентификатор устройства для этой подписки</param>
/// <param name="SendDeviceInfo">Сообщать модель и версию роутера</param>
/// <param name="ServerCount">Сколько серверов у подписки</param>
/// <param name="Provider">Сведения от провайдера или null</param>
/// <param name="LastRefresh">Последнее обновление или null</param>
/// <param name="NextRefreshAt">Следующее обновление или null, если автообновление выключено</param>
/// <param name="CreatedAt">Когда добавлена</param>
/// <param name="RefreshJobId">Задача обновления, запущенная этим запросом, или null</param>
public sealed record SubscriptionDto(
    string Id,
    string DisplayName,
    string? Name,
    string Url,
    bool AutoUpdate,
    int? UpdateIntervalHours,
    double EffectiveIntervalHours,
    string? UserAgent,
    bool SendHwid,
    string Hwid,
    bool SendDeviceInfo,
    int ServerCount,
    ProviderMetadataDto? Provider,
    SubscriptionRefreshDto? LastRefresh,
    DateTimeOffset? NextRefreshAt,
    DateTimeOffset CreatedAt,
    Guid? RefreshJobId);
