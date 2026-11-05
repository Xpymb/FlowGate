namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Сохранение настроек целиком
/// </summary>
/// <param name="XrayLogLevel">Уровень логирования Xray</param>
/// <param name="UpdateProxy">Загрузка обновлений через прокси</param>
/// <param name="Intervals">Интервалы обновлений</param>
public sealed record SettingsSaveRequest(
    string XrayLogLevel,
    UpdateProxySaveRequest UpdateProxy,
    UpdateIntervalsSaveRequest Intervals);
