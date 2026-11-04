using System.Text.Json;
using System.Text.Json.Serialization;

using FlowGate.SharedContracts.Dto.Health;
using FlowGate.SharedContracts.Dto.Keenetic;

namespace FlowGate.WebApi.Serialization;

/// <summary>
///     Source-generated контекст сериализации DTO API. Каждый новый DTO эндпоинта добавляется сюда
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(KeeneticSystemInfoDto))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
