namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Настройки прокси обновлений
/// </summary>
/// <param name="Enabled">Включить</param>
/// <param name="ServerId">Сервер; обязателен, если прокси включён</param>
/// <param name="ExtraDomains">Свои домены: <c>example.com</c>, <c>domain:example.com</c> или <c>full:cdn.example.com</c></param>
public sealed record UpdateProxySaveRequest(bool Enabled, string? ServerId = null, IReadOnlyList<string>? ExtraDomains = null);
