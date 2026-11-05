using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор ссылки на сервер по её схеме
/// </summary>
public static class ServerLinkParser
{
    /// <summary>
    ///     Поддерживаемые схемы
    /// </summary>
    public static IReadOnlyList<string> Schemes { get; } = ["vless", "vmess", "trojan", "ss", "hysteria2", "hy2"];

    /// <summary>
    ///     Разбирает ссылку
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер</returns>
    /// <exception cref="ServerLinkFormatException">Если схема не поддерживается или ссылка некорректна</exception>
    public static ParsedServer Parse(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var trimmed = link.Trim();
        var schemeEnd = trimmed.IndexOf("://", StringComparison.Ordinal);
        var scheme = schemeEnd > 0 ? trimmed[..schemeEnd].ToLowerInvariant() : string.Empty;

        return scheme switch
        {
            "vless" => VlessLinkParser.Parse(trimmed),
            "vmess" => VmessLinkParser.Parse(trimmed),
            "trojan" => TrojanLinkParser.Parse(trimmed),
            "ss" => ShadowsocksLinkParser.Parse(trimmed),
            "hysteria2" or "hy2" => Hysteria2LinkParser.Parse(trimmed),
            "" => throw new ServerLinkFormatException("Это не ссылка на сервер"),
            _ => throw new ServerLinkFormatException($"Схема «{scheme}://» не поддерживается"),
        };
    }
}
