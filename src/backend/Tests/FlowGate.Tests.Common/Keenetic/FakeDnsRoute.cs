namespace FlowGate.Tests.Common.Keenetic;

/// <summary>
///     Правило <c>dns-proxy route</c> в подменном Keenetic
/// </summary>
/// <param name="Group">Группа</param>
/// <param name="Interface">Интерфейс</param>
/// <param name="Auto">Флаг <c>auto</c></param>
/// <param name="Disabled">Выключено</param>
public sealed record FakeDnsRoute(string Group, string Interface, bool Auto, bool Disabled);
