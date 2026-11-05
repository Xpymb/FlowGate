namespace FlowGate.Contracts.Connections;

/// <summary>
///     Общие параметры интерфейсов Keenetic, которые не хранятся в соединении, а берутся из настроек
/// </summary>
/// <param name="UpstreamAddress">Адрес inbound Xray, к которому подключается интерфейс</param>
/// <param name="WireguardMtu">MTU WireGuard-интерфейса</param>
/// <param name="WireguardKeepaliveSeconds">Интервал keepalive пира WireGuard</param>
public sealed record KeeneticInterfaceParameters(string UpstreamAddress, int WireguardMtu, int WireguardKeepaliveSeconds);
