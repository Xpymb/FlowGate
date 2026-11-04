using System.Text.Json.Serialization;

namespace FlowGate.Shared.Keenetic.Serialization;

/// <summary>
///     Source-generated контекст сериализации запросов RCI
/// </summary>
[JsonSerializable(typeof(List<RciParseRequest>))]
public sealed partial class KeeneticRciJsonContext : JsonSerializerContext
{
}
