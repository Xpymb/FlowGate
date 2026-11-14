using System.Text.Json;
using System.Text.Json.Serialization;

using FlowGate.SharedContracts.Dto.Connections;
using FlowGate.SharedContracts.Dto.DnsRoutes;
using FlowGate.SharedContracts.Dto.DomainSources;
using FlowGate.SharedContracts.Dto.Health;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Keenetic;
using FlowGate.SharedContracts.Dto.Maintenance;
using FlowGate.SharedContracts.Dto.Servers;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.SharedContracts.Dto.Subscriptions;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.WebApi.Serialization;

/// <summary>
///     Source-generated контекст сериализации DTO API. Каждый новый DTO эндпоинта добавляется сюда
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(HealthDto))]
[JsonSerializable(typeof(SettingsDto))]
[JsonSerializable(typeof(SettingsSaveRequest))]
[JsonSerializable(typeof(UpdateProxyStatusDto))]
[JsonSerializable(typeof(SystemStatusDto))]
[JsonSerializable(typeof(KeeneticSystemInfoDto))]
[JsonSerializable(typeof(XrayStatusDto))]
[JsonSerializable(typeof(IReadOnlyList<XrayReleaseDto>))]
[JsonSerializable(typeof(XrayInstallRequest))]
[JsonSerializable(typeof(XrayLogDto))]
[JsonSerializable(typeof(JobDto))]
[JsonSerializable(typeof(IReadOnlyList<JobDto>))]
[JsonSerializable(typeof(IReadOnlyList<ServerDto>))]
[JsonSerializable(typeof(ServerDetailsDto))]
[JsonSerializable(typeof(ServerParseRequest))]
[JsonSerializable(typeof(ServerDraftDto))]
[JsonSerializable(typeof(ServerValidateRequest))]
[JsonSerializable(typeof(ServerValidationDto))]
[JsonSerializable(typeof(ServerSaveRequest))]
[JsonSerializable(typeof(ServerImportRequest))]
[JsonSerializable(typeof(ServerImportResultDto))]
[JsonSerializable(typeof(ServerPreviewDto))]
[JsonSerializable(typeof(IReadOnlyList<SubscriptionDto>))]
[JsonSerializable(typeof(SubscriptionDto))]
[JsonSerializable(typeof(SubscriptionSaveRequest))]
[JsonSerializable(typeof(IReadOnlyList<ConnectionDto>))]
[JsonSerializable(typeof(ConnectionDto))]
[JsonSerializable(typeof(ConnectionCreateRequest))]
[JsonSerializable(typeof(ConnectionUpdateRequest))]
[JsonSerializable(typeof(IReadOnlyList<DomainSourceDto>))]
[JsonSerializable(typeof(DomainSourceDto))]
[JsonSerializable(typeof(DomainSourceSaveRequest))]
[JsonSerializable(typeof(IReadOnlyList<DomainListSummaryDto>))]
[JsonSerializable(typeof(DomainListPreviewDto))]
[JsonSerializable(typeof(IReadOnlyList<LocalListDto>))]
[JsonSerializable(typeof(LocalListDto))]
[JsonSerializable(typeof(LocalListSaveRequest))]
[JsonSerializable(typeof(IReadOnlyList<DnsRouteDto>))]
[JsonSerializable(typeof(DnsRouteDto))]
[JsonSerializable(typeof(DnsRouteCreateRequest))]
[JsonSerializable(typeof(DnsRouteUpdateRequest))]
public sealed partial class AppJsonSerializerContext : JsonSerializerContext
{
}
