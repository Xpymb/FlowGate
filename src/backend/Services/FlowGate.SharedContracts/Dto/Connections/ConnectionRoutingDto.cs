namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Через какой сервер сейчас идёт трафик соединения
/// </summary>
/// <param name="Status"><c>Active</c>, <c>Direct</c> (сервер один), <c>Blocked</c>, <c>Unsupported</c> (Xray без API) или <c>Unavailable</c></param>
/// <param name="ActiveServerId">Сервер, через который идёт трафик, при <c>Active</c> и <c>Direct</c></param>
/// <param name="ActiveServerName">Его имя</param>
/// <param name="ActiveSlot">0 — основной, 1..3 — номер резервного</param>
public sealed record ConnectionRoutingDto(string Status, string? ActiveServerId, string? ActiveServerName, int? ActiveSlot);
