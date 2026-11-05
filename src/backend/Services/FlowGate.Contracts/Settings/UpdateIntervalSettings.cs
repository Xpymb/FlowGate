namespace FlowGate.Contracts.Settings;

/// <summary>
///     Интервалы обновлений в часах; null — значение из конфигурации (<c>appsettings.json</c>)
/// </summary>
public sealed record UpdateIntervalSettings
{
    /// <summary>
    ///     Все интервалы из конфигурации
    /// </summary>
    public static UpdateIntervalSettings Default { get; } = new();

    /// <summary>
    ///     Наибольший интервал обновления подписок, часы (как у интервала отдельной подписки)
    /// </summary>
    public const int MaxSubscriptionHours = 168;

    /// <summary>
    ///     Наибольший интервал обновления источников и проверки версий, часы
    /// </summary>
    public const int MaxHours = 720;

    /// <summary>
    ///     Интервал обновления подписок, у которых не задан свой и нет интервала от провайдера
    /// </summary>
    public int? SubscriptionDefaultHours { get; init; }

    /// <summary>
    ///     Интервал обновления источников списков
    /// </summary>
    public int? DomainSourceRefreshHours { get; init; }

    /// <summary>
    ///     Интервал проверки новых версий Xray
    /// </summary>
    public int? XrayUpdateCheckHours { get; init; }

    /// <summary>
    ///     Интервал проверки новых версий FlowGate
    /// </summary>
    public int? SystemUpdateCheckHours { get; init; }
}
