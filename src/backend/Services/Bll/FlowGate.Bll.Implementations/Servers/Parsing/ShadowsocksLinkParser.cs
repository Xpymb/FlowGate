using System.Text;
using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор Shadowsocks по SIP002: <c>ss://method:password@host:port#name</c> (userinfo в percent-кодировке или base64)
///     и устаревшая форма <c>ss://base64(method:password@host:port)#name</c>. Плагины SIP003 не поддерживаются
/// </summary>
public static class ShadowsocksLinkParser
{
    private static readonly HashSet<string> Methods = new(StringComparer.Ordinal)
    {
        "aes-128-gcm",
        "aes-256-gcm",
        "chacha20-poly1305",
        "chacha20-ietf-poly1305",
        "xchacha20-poly1305",
        "xchacha20-ietf-poly1305",
        "2022-blake3-aes-128-gcm",
        "2022-blake3-aes-256-gcm",
        "2022-blake3-chacha20-poly1305",
        "none",
        "plain",
    };

    /// <summary>
    ///     Разбирает ссылку Shadowsocks
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер с outbound <c>shadowsocks</c></returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна, метод неизвестен или указан плагин</exception>
    public static ParsedServer Parse(string link)
    {
        var parts = ShareLink.Parse(ExpandLegacy(link));

        if (parts.Get("plugin") is not null)
        {
            throw new ServerLinkFormatException("Плагины Shadowsocks (SIP003) не поддерживаются");
        }

        var (method, password) = ParseUserInfo(parts.UserInfo);
        method = method.ToLowerInvariant();
        if (!Methods.Contains(method))
        {
            throw new ServerLinkFormatException($"Метод шифрования Shadowsocks «{method}» не поддерживается");
        }

        if (method.StartsWith("2022-", StringComparison.Ordinal))
        {
            ValidateKeys(method, password);
        }

        return new ParsedServer(
            parts.DisplayName,
            new JsonObject
            {
                ["protocol"] = "shadowsocks",
                ["settings"] = new JsonObject
                {
                    ["address"] = parts.Host,
                    ["port"] = parts.Port,
                    ["method"] = method,
                    ["password"] = password,
                },
            });
    }

    private static string ExpandLegacy(string link)
    {
        var schemeEnd = link.IndexOf("://", StringComparison.Ordinal);
        var body = link[(schemeEnd + 3)..];
        var tailIndex = body.IndexOfAny(['#', '?']);
        var authority = tailIndex >= 0 ? body[..tailIndex] : body;
        var tail = tailIndex >= 0 ? body[tailIndex..] : string.Empty;

        if (authority.Contains('@') || !ShareLink.TryDecodeBase64(authority.TrimEnd('/'), out var bytes))
        {
            return link;
        }

        var decoded = Encoding.UTF8.GetString(bytes);
        var at = decoded.LastIndexOf('@');
        if (at <= 0)
        {
            throw new ServerLinkFormatException("Некорректная ссылка Shadowsocks");
        }

        var userInfo = Uri.EscapeDataString(decoded[..at]).Replace("%3A", ":", StringComparison.Ordinal);
        return $"ss://{userInfo}@{decoded[(at + 1)..]}{tail}";
    }

    private static (string Method, string Password) ParseUserInfo(string userInfo)
    {
        var plain = ShareLink.Decode(userInfo);
        var colon = plain.IndexOf(':');
        if (colon < 0)
        {
            if (!ShareLink.TryDecodeBase64(userInfo, out var bytes))
            {
                throw new ServerLinkFormatException("Userinfo ссылки Shadowsocks — не method:password и не base64");
            }

            plain = Encoding.UTF8.GetString(bytes);
            colon = plain.IndexOf(':');
        }

        if (colon <= 0 || colon == plain.Length - 1)
        {
            throw new ServerLinkFormatException("В ссылке Shadowsocks нужны метод и пароль");
        }

        return (plain[..colon], plain[(colon + 1)..]);
    }

    private static void ValidateKeys(string method, string password)
    {
        var expectedLength = method == "2022-blake3-aes-128-gcm" ? 16 : 32;
        foreach (var key in password.Split(':'))
        {
            if (!ShareLink.TryDecodeBase64(key, out var bytes) || bytes.Length != expectedLength)
            {
                throw new ServerLinkFormatException($"Ключ {method} должен быть base64 от {expectedLength} байт");
            }
        }
    }
}
