namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Результат проверки outbound
/// </summary>
/// <param name="Valid">Ошибок нет</param>
/// <param name="CheckedByXray">Проверен установленным Xray; false — Xray не установлен, проверена только структура</param>
/// <param name="Errors">Ошибки проверки</param>
public sealed record ServerValidationDto(bool Valid, bool CheckedByXray, IReadOnlyList<string> Errors);
