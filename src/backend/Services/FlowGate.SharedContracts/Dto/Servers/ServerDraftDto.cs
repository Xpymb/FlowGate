using System.Text.Json.Nodes;

namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Черновик сервера после разбора ссылки: подставляется в JSON-редактор
/// </summary>
/// <param name="Name">Имя из ссылки</param>
/// <param name="Outbound">Outbound Xray без <c>tag</c></param>
/// <param name="Warnings">Предупреждения разбора</param>
public sealed record ServerDraftDto(string Name, JsonObject Outbound, IReadOnlyList<string> Warnings);
