namespace FlowGate.SharedContracts.Dto.Subscriptions;

/// <summary>
///     Создание или изменение подписки
/// </summary>
/// <param name="Url">Адрес подписки (http или https)</param>
/// <param name="Name">Своё имя или null — имя провайдера</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
/// <param name="UpdateIntervalHours">Свой интервал 1–168 ч или null — интервал провайдера или 24 ч</param>
/// <param name="UserAgent">Свой User-Agent или null — <c>FlowGate/&lt;версия&gt;</c></param>
/// <param name="SendHwid">Отправлять постоянный <c>x-hwid</c></param>
/// <param name="SendDeviceInfo">Сообщать провайдеру модель и версию роутера</param>
public sealed record SubscriptionSaveRequest(
    string Url,
    string? Name = null,
    bool AutoUpdate = true,
    int? UpdateIntervalHours = null,
    string? UserAgent = null,
    bool SendHwid = true,
    bool SendDeviceInfo = false);
