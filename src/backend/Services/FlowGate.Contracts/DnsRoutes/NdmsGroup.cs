namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Группа <c>object-group fqdn</c> в Keenetic
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Description">Описание</param>
/// <param name="Entries">Элементы в записи Keenetic (как были добавлены)</param>
public sealed record NdmsGroup(string Name, string Description, IReadOnlyList<string> Entries);
