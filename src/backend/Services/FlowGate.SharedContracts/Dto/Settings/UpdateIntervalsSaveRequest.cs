namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Интервалы обновлений в часах; null — значение из конфигурации
/// </summary>
/// <param name="SubscriptionDefaultHours">Подписки без своего интервала, 1–168</param>
/// <param name="DomainSourceRefreshHours">Источники списков, 1–720</param>
/// <param name="XrayUpdateCheckHours">Проверка версий Xray, 1–720</param>
/// <param name="SystemUpdateCheckHours">Проверка версий FlowGate, 1–720</param>
public sealed record UpdateIntervalsSaveRequest(
    int? SubscriptionDefaultHours = null,
    int? DomainSourceRefreshHours = null,
    int? XrayUpdateCheckHours = null,
    int? SystemUpdateCheckHours = null);
