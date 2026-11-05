using FlowGate.Contracts.Settings;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Settings;

/// <summary>
///     Действующие интервалы обновлений: значение из настроек пользователя, иначе из конфигурации
/// </summary>
public static class UpdateIntervals
{
    /// <summary>
    ///     Интервал подписок без своего интервала и без интервала от провайдера
    /// </summary>
    /// <param name="settings">Настройки пользователя</param>
    /// <param name="options">Настройки подписок</param>
    /// <returns>Интервал</returns>
    public static TimeSpan SubscriptionDefault(AppSettings settings, SubscriptionOptions options)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        return TimeSpan.FromHours(settings.Intervals.SubscriptionDefaultHours ?? options.DefaultUpdateIntervalHours);
    }

    /// <summary>
    ///     Интервал обновления источников списков
    /// </summary>
    /// <param name="settings">Настройки пользователя</param>
    /// <param name="options">Настройки источников</param>
    /// <returns>Интервал</returns>
    public static TimeSpan DomainSourceRefresh(AppSettings settings, DomainSourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        return TimeSpan.FromHours(settings.Intervals.DomainSourceRefreshHours ?? options.RefreshIntervalHours);
    }

    /// <summary>
    ///     Интервал проверки новых версий Xray
    /// </summary>
    /// <param name="settings">Настройки пользователя</param>
    /// <param name="options">Настройки Xray</param>
    /// <returns>Интервал</returns>
    public static TimeSpan XrayUpdateCheck(AppSettings settings, XrayOptions options)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        return TimeSpan.FromHours(settings.Intervals.XrayUpdateCheckHours ?? options.UpdateCheckIntervalHours);
    }

    /// <summary>
    ///     Интервал проверки новых версий FlowGate
    /// </summary>
    /// <param name="settings">Настройки пользователя</param>
    /// <param name="options">Настройки FlowGate</param>
    /// <returns>Интервал</returns>
    public static TimeSpan SystemUpdateCheck(AppSettings settings, SystemOptions options)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);
        return TimeSpan.FromHours(settings.Intervals.SystemUpdateCheckHours ?? options.UpdateCheckIntervalHours);
    }

    /// <summary>
    ///     Пора ли проверять снова
    /// </summary>
    /// <param name="lastCheckAt">Время последней успешной проверки или null</param>
    /// <param name="interval">Интервал</param>
    /// <param name="now">Текущее время</param>
    /// <returns>true, если проверки не было или интервал истёк</returns>
    public static bool IsDue(DateTimeOffset? lastCheckAt, TimeSpan interval, DateTimeOffset now) =>
        lastCheckAt is null || lastCheckAt.Value + interval <= now;
}
