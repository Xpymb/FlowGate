namespace FlowGate.SharedContracts.Dto.Xray;

/// <summary>
///     Запрос установки Xray
/// </summary>
/// <param name="Version">Версия из списка релизов; null — последняя стабильная</param>
public sealed record XrayInstallRequest(string? Version);
