using System.Text.Json;
using System.Text.Json.Serialization;

using FlowGate.Contracts.Settings;

namespace FlowGate.Dal.Implementations.Serialization;

/// <summary>
///     Source-generated контекст сериализации файлов данных
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true)]
[JsonSerializable(typeof(AppSettings))]
public sealed partial class DalJsonContext : JsonSerializerContext
{
}
