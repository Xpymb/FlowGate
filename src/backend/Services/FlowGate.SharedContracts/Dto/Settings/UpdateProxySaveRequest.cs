namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Настройки прокси обновлений
/// </summary>
/// <param name="Enabled">Включить</param>
/// <param name="ServerId">Сервер; обязателен, если прокси включён</param>
public sealed record UpdateProxySaveRequest(bool Enabled, string? ServerId = null);
