using System.Text.Json;
using System.Text.Json.Serialization;

using FlowGate.Contracts.Connections;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Contracts.DomainSources;
using FlowGate.Contracts.Servers;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.Subscriptions;

namespace FlowGate.Dal.Implementations.Serialization;

/// <summary>
///     Source-generated контекст сериализации файлов данных. Перечисления пишутся строками, чтобы файлы читались человеком
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, WriteIndented = true, UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
[JsonSerializable(typeof(ServerCatalog))]
[JsonSerializable(typeof(SubscriptionCatalog))]
[JsonSerializable(typeof(ConnectionCatalog))]
[JsonSerializable(typeof(DomainSourceCatalog))]
[JsonSerializable(typeof(LocalListCatalog))]
[JsonSerializable(typeof(SourceListCatalog))]
[JsonSerializable(typeof(DnsRouteCatalog))]
public sealed partial class DalJsonContext : JsonSerializerContext
{
}
