namespace FlowGate.Contracts.Xray;

/// <summary>
///     Результат проверки обновлений Xray
/// </summary>
/// <param name="LatestVersion">Последняя стабильная версия или null, если подходящих релизов нет</param>
/// <param name="CheckedAt">Время проверки</param>
public sealed record XrayUpdateCheck(XrayVersion? LatestVersion, DateTimeOffset CheckedAt);
