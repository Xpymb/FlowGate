namespace FlowGate.SharedContracts.Dto.Xray;

/// <summary>
///     Строка вывода Xray
/// </summary>
/// <param name="Timestamp">Когда строка получена</param>
/// <param name="Text">Текст строки</param>
public sealed record XrayLogLineDto(DateTimeOffset Timestamp, string Text);
