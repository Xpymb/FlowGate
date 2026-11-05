namespace FlowGate.Bll.Implementations.Settings;

/// <summary>
///     Интервалы обновлений из конфигурации, часы
/// </summary>
/// <param name="SubscriptionDefaultHours">Подписки</param>
/// <param name="DomainSourceRefreshHours">Источники списков</param>
/// <param name="XrayUpdateCheckHours">Проверка версий Xray</param>
/// <param name="SystemUpdateCheckHours">Проверка версий FlowGate</param>
public sealed record IntervalDefaults(
    int SubscriptionDefaultHours,
    int DomainSourceRefreshHours,
    int XrayUpdateCheckHours,
    int SystemUpdateCheckHours);
