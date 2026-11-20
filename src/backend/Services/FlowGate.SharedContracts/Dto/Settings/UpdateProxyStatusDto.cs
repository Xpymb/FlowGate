namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Состояние прокси обновлений
/// </summary>
/// <param name="State"><c>Disabled</c>, <c>Ready</c> (inbound в основном Xray), <c>Unavailable</c> (обновления идут напрямую)</param>
/// <param name="Port">Порт HTTP-inbound на 127.0.0.1</param>
/// <param name="Reason">Почему недоступен или последний сбой</param>
public sealed record UpdateProxyStatusDto(string State, int Port, string? Reason);
