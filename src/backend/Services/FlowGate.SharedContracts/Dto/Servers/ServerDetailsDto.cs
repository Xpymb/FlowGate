using System.Text.Json.Nodes;

namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Сервер с outbound для JSON-редактора
/// </summary>
/// <param name="Server">Сводка сервера</param>
/// <param name="Outbound">Outbound Xray без <c>tag</c></param>
/// <param name="Editable">Можно править: ручной сервер (серверы подписки перезаписываются при её обновлении)</param>
public sealed record ServerDetailsDto(ServerDto Server, JsonObject Outbound, bool Editable);
