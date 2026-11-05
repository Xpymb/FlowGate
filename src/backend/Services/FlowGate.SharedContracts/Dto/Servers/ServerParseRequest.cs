namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Запрос разбора ссылки
/// </summary>
/// <param name="Text">Ссылка (vless://, vmess://, trojan://, ss://, hysteria2://, hy2://) или Xray-JSON</param>
public sealed record ServerParseRequest(string Text);
