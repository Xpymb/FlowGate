namespace FlowGate.SharedContracts.Dto.Health;

/// <summary>
///     Ответ проверки живости приложения
/// </summary>
/// <param name="Status">Всегда <c>ok</c>, если приложение отвечает</param>
/// <param name="Version">Версия FlowGate</param>
public sealed record HealthDto(string Status, string Version);
