namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Настройки FlowGate, которые меняются из интерфейса
/// </summary>
/// <param name="XrayLogLevel">Уровень логирования Xray</param>
/// <param name="XrayLogLevels">Допустимые уровни логирования</param>
/// <param name="UpdateProxy">Загрузка обновлений через прокси</param>
/// <param name="Intervals">Интервалы обновлений</param>
public sealed record SettingsDto(
    string XrayLogLevel,
    IReadOnlyList<string> XrayLogLevels,
    UpdateProxySettingsDto UpdateProxy,
    UpdateIntervalsDto Intervals);
