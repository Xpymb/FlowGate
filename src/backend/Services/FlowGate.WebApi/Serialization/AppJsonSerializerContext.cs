using System.Text.Json;
using System.Text.Json.Serialization;

using FlowGate.SharedContracts.Dto.Health;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Keenetic;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.WebApi.Serialization;

/// <summary>
///     Source-generated контекст сериализации DTO API. Каждый новый DTO эндпоинта добавляется сюда
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(KeeneticSystemInfoDto))]
[JsonSerializable(typeof(XrayStatusDto))]
[JsonSerializable(typeof(IReadOnlyList<XrayReleaseDto>))]
[JsonSerializable(typeof(XrayInstallRequest))]
[JsonSerializable(typeof(XrayLogDto))]
[JsonSerializable(typeof(JobDto))]
[JsonSerializable(typeof(IReadOnlyList<JobDto>))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
