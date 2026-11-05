namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Интервалы обновлений
/// </summary>
/// <param name="SubscriptionDefault">Подписки без своего интервала и без интервала от провайдера</param>
/// <param name="DomainSourceRefresh">Источники списков</param>
/// <param name="XrayUpdateCheck">Проверка новых версий Xray</param>
/// <param name="SystemUpdateCheck">Проверка новых версий FlowGate</param>
public sealed record UpdateIntervalsDto(
    UpdateIntervalDto SubscriptionDefault,
    UpdateIntervalDto DomainSourceRefresh,
    UpdateIntervalDto XrayUpdateCheck,
    UpdateIntervalDto SystemUpdateCheck);
