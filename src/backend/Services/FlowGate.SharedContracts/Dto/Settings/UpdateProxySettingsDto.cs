namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Загрузка обновлений (подписки, источники, релизы Xray, пакет FlowGate) через выбранный сервер в основном Xray
/// </summary>
/// <param name="Enabled">Включено</param>
/// <param name="ServerId">Сервер, через который идут обновления</param>
public sealed record UpdateProxySettingsDto(bool Enabled, string? ServerId);
