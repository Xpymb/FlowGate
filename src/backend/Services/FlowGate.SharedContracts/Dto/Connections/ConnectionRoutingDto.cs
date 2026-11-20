namespace FlowGate.SharedContracts.Dto.Connections;

/// <summary>
///     Через какой сервер сейчас идёт трафик соединения
/// </summary>
/// <param name="Status">
///     <c>Active</c> — выбор балансировщика Xray известен; <c>Direct</c> — живой сервер один, балансировщика нет;
///     <c>Blocked</c> — серверов нет, трафик блокируется; <c>Unsupported</c> — установленная версия Xray не даёт API
///     балансировщика; <c>Unavailable</c> — сейчас узнать нельзя (Xray не запущен, API не ответил)
/// </param>
/// <param name="ActiveServerId">Сервер, через который идёт трафик, при <c>Active</c> и <c>Direct</c></param>
/// <param name="ActiveServerName">Его имя</param>
/// <param name="ActiveSlot">0 — основной, 1..3 — номер резервного</param>
public sealed record ConnectionRoutingDto(string Status, string? ActiveServerId, string? ActiveServerName, int? ActiveSlot);
