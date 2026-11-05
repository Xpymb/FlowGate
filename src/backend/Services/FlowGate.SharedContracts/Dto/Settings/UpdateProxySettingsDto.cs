namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Загрузка обновлений (подписки, источники, релизы Xray, пакет FlowGate) через отдельный экземпляр Xray
/// </summary>
/// <param name="Enabled">Включено</param>
/// <param name="ServerId">Сервер, через который идут обновления</param>
/// <param name="ExtraDomains">Свои домены в формате Xray</param>
/// <param name="BuiltInDomains">Встроенные домены (GitHub, feed); хосты подписок и прямых ссылок источников добавляются сами</param>
public sealed record UpdateProxySettingsDto(
    bool Enabled,
    string? ServerId,
    IReadOnlyList<string> ExtraDomains,
    IReadOnlyList<string> BuiltInDomains);
