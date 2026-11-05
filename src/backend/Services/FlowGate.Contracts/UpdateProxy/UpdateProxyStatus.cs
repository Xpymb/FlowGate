namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Снимок состояния прокси обновлений
/// </summary>
/// <param name="State">Состояние</param>
/// <param name="Port">Порт HTTP-inbound</param>
/// <param name="ProcessId">Pid экземпляра или null</param>
/// <param name="StartedAt">Когда экземпляр запущен</param>
/// <param name="ActiveRequests">Сколько запросов идёт через прокси сейчас</param>
/// <param name="Reason">Почему прокси недоступен или последний сбой</param>
public sealed record UpdateProxyStatus(
    UpdateProxyState State,
    int Port,
    int? ProcessId,
    DateTimeOffset? StartedAt,
    int ActiveRequests,
    string? Reason);
