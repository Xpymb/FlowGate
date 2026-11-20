namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Снимок состояния прокси обновлений
/// </summary>
/// <param name="State">Состояние</param>
/// <param name="Port">Порт HTTP-inbound</param>
/// <param name="Reason">Почему прокси недоступен или последний сбой</param>
public sealed record UpdateProxyStatus(UpdateProxyState State, int Port, string? Reason);
