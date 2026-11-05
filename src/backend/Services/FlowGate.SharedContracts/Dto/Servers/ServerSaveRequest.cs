using System.Text.Json.Nodes;

namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Создание или изменение ручного сервера
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Outbound">Outbound Xray; поле <c>tag</c>, если есть, отбрасывается</param>
public sealed record ServerSaveRequest(string Name, JsonObject Outbound);
