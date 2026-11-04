namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Пакетный импорт серверов
/// </summary>
/// <param name="Text">Ссылки по одной на строку, base64 от них или Xray-JSON</param>
public sealed record ServerImportRequest(string Text);
