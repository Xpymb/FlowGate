namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Правило <c>dns-proxy route</c> в Keenetic
/// </summary>
/// <param name="Group">Группа</param>
/// <param name="Interface">Интерфейс</param>
/// <param name="Auto">Флаг <c>auto</c></param>
/// <param name="Disabled">Правило выключено</param>
public sealed record NdmsRoute(string Group, string Interface, bool Auto, bool Disabled);
