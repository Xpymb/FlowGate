using System.Text.Json.Nodes;

namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Запрос проверки outbound
/// </summary>
/// <param name="Outbound">Outbound Xray</param>
public sealed record ServerValidateRequest(JsonObject Outbound);
