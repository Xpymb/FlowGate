using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Servers;

/// <summary>
///     Сервер, разобранный из ссылки или Xray-JSON, ещё не сохранённый
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Outbound">Outbound Xray без поля <c>tag</c></param>
public sealed record ParsedServer(string Name, JsonObject Outbound);
