using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.Settings;

/// <summary>
///     Маппинг настроек и состояния прокси обновлений в DTO
/// </summary>
public static class SettingsMapper
{
    /// <summary>
    ///     Настройки → DTO с действующими интервалами
    /// </summary>
    /// <param name="settings">Настройки пользователя</param>
    /// <param name="defaults">Интервалы из конфигурации</param>
    /// <returns>DTO</returns>
    public static SettingsDto ToDto(AppSettings settings, IntervalDefaults defaults)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(defaults);

        var intervals = settings.Intervals;
        return new SettingsDto(
            settings.XrayLogLevel,
            XrayConfigModel.LogLevels,
            new UpdateProxySettingsDto(
                settings.UpdateProxy.Enabled,
                settings.UpdateProxy.ServerId,
                settings.UpdateProxy.ExtraDomains,
                UpdateProxyDomains.BuiltIn),
            new UpdateIntervalsDto(
                Interval(intervals.SubscriptionDefaultHours, defaults.SubscriptionDefaultHours, UpdateIntervalSettings.MaxSubscriptionHours),
                Interval(intervals.DomainSourceRefreshHours, defaults.DomainSourceRefreshHours, UpdateIntervalSettings.MaxHours),
                Interval(intervals.XrayUpdateCheckHours, defaults.XrayUpdateCheckHours, UpdateIntervalSettings.MaxHours),
                Interval(intervals.SystemUpdateCheckHours, defaults.SystemUpdateCheckHours, UpdateIntervalSettings.MaxHours)));
    }

    /// <summary>
    ///     Состояние прокси обновлений → DTO
    /// </summary>
    /// <param name="status">Состояние</param>
    /// <returns>DTO</returns>
    public static UpdateProxyStatusDto ToDto(UpdateProxyStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);
        return new UpdateProxyStatusDto(
            status.State.ToString(),
            status.Port,
            status.ProcessId,
            status.StartedAt,
            status.ActiveRequests,
            status.Reason);
    }

    private static UpdateIntervalDto Interval(int? hours, int defaultHours, int maxHours) =>
        new(hours, defaultHours, hours ?? defaultHours, maxHours);
}
