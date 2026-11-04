using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowGate.Shared.WebApi.Errors;

/// <summary>
///     Source-generated контекст сериализации ответов об ошибках
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ErrorResponse))]
public sealed partial class ErrorJsonContext : JsonSerializerContext
{
}
