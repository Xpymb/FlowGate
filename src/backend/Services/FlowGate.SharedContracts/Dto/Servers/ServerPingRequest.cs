namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Пинг серверов
/// </summary>
/// <param name="ServerIds">Серверы; за один запрос не больше <c>Ping:MaxServersPerRequest</c></param>
public sealed record ServerPingRequest(IReadOnlyList<string> ServerIds);
