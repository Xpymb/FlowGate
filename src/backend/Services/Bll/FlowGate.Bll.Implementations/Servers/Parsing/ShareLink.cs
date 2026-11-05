using System.Globalization;
using System.Text;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Части ссылки <c>scheme://userinfo@host:port/path?query#fragment</c>. Разбор ручной: <see cref="Uri"/> портит
///     base64 в userinfo (символы <c>/</c>, <c>+</c>, <c>=</c>) и IPv6
/// </summary>
/// <param name="Scheme">Схема в нижнем регистре</param>
/// <param name="UserInfo">Userinfo как есть, без декодирования</param>
/// <param name="Host">Адрес без скобок IPv6</param>
/// <param name="Port">Порт</param>
/// <param name="Path">Путь (с ведущим <c>/</c>) или пустая строка</param>
/// <param name="Query">Параметры query, декодированные, ключи без учёта регистра; при повторе берётся первый</param>
/// <param name="Fragment">Фрагмент (обычно имя сервера), декодированный</param>
public sealed record ShareLink(
    string Scheme,
    string UserInfo,
    string Host,
    int Port,
    string Path,
    IReadOnlyDictionary<string, string> Query,
    string Fragment)
{
    /// <summary>
    ///     Имя сервера: фрагмент ссылки или <c>адрес:порт</c>
    /// </summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Fragment) ? $"{Host}:{Port}" : Fragment.Trim();

    /// <summary>
    ///     Разбирает ссылку
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Части ссылки</returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна</exception>
    public static ShareLink Parse(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var schemeEnd = link.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd <= 0)
        {
            throw new ServerLinkFormatException("Ссылка должна начинаться со схемы, например vless://");
        }

        var scheme = link[..schemeEnd].ToLowerInvariant();
        var rest = link[(schemeEnd + 3)..];

        var fragment = string.Empty;
        var hashIndex = rest.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = Decode(rest[(hashIndex + 1)..]);
            rest = rest[..hashIndex];
        }

        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var queryIndex = rest.IndexOf('?');
        if (queryIndex >= 0)
        {
            ParseQuery(rest[(queryIndex + 1)..], query);
            rest = rest[..queryIndex];
        }

        var atIndex = rest.LastIndexOf('@');
        var userInfo = atIndex >= 0 ? rest[..atIndex] : string.Empty;
        var hostPortPath = atIndex >= 0 ? rest[(atIndex + 1)..] : rest;

        var pathIndex = hostPortPath.IndexOf('/');
        var path = pathIndex >= 0 ? hostPortPath[pathIndex..] : string.Empty;
        var hostPort = pathIndex >= 0 ? hostPortPath[..pathIndex] : hostPortPath;

        var (host, port) = ParseHostPort(hostPort);
        return new ShareLink(scheme, userInfo, host, port, path, query, fragment);
    }

    /// <summary>
    ///     Возвращает параметр query или null, если его нет или он пустой
    /// </summary>
    /// <param name="names">Имена параметра; берётся первое найденное</param>
    /// <returns>Значение или null</returns>
    public string? Get(params string[] names)
    {
        foreach (var name in names)
        {
            if (Query.TryGetValue(name, out var value) && value.Length > 0)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    ///     Percent-декодирование без превращения <c>+</c> в пробел
    /// </summary>
    /// <param name="value">Строка</param>
    /// <returns>Декодированная строка</returns>
    /// <exception cref="ServerLinkFormatException">Если кодировка некорректна</exception>
    public static string Decode(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            throw new ServerLinkFormatException("Некорректное percent-кодирование в ссылке");
        }
    }

    /// <summary>
    ///     Декодирует base64 и base64url, с паддингом и без
    /// </summary>
    /// <param name="value">Строка base64</param>
    /// <param name="bytes">Декодированные байты</param>
    /// <returns>true, если строка — корректный base64</returns>
    public static bool TryDecodeBase64(string value, out byte[] bytes)
    {
        var normalized = new StringBuilder(value.Length + 3);
        foreach (var symbol in value)
        {
            if (char.IsWhiteSpace(symbol))
            {
                continue;
            }

            normalized.Append(symbol switch
            {
                '-' => '+',
                '_' => '/',
                _ => symbol,
            });
        }

        while (normalized.Length % 4 != 0)
        {
            normalized.Append('=');
        }

        bytes = new byte[normalized.Length];
        if (normalized.Length > 0 && Convert.TryFromBase64String(normalized.ToString(), bytes, out var written))
        {
            bytes = bytes[..written];
            return true;
        }

        bytes = [];
        return false;
    }

    private static void ParseQuery(string query, Dictionary<string, string> target)
    {
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = Decode(separator >= 0 ? pair[..separator] : pair);
            var value = separator >= 0 ? Decode(pair[(separator + 1)..]) : string.Empty;
            target.TryAdd(key, value);
        }
    }

    private static (string Host, int Port) ParseHostPort(string hostPort)
    {
        string host;
        string portText;
        if (hostPort.StartsWith('['))
        {
            var close = hostPort.IndexOf(']');
            if (close < 0 || close + 1 >= hostPort.Length || hostPort[close + 1] != ':')
            {
                throw new ServerLinkFormatException("Некорректный адрес IPv6 в ссылке");
            }

            host = hostPort[1..close];
            portText = hostPort[(close + 2)..];
        }
        else
        {
            var colon = hostPort.LastIndexOf(':');
            if (colon <= 0)
            {
                throw new ServerLinkFormatException("В ссылке нет порта");
            }

            host = hostPort[..colon];
            portText = hostPort[(colon + 1)..];
        }

        if (host.Length == 0)
        {
            throw new ServerLinkFormatException("В ссылке нет адреса сервера");
        }

        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw new ServerLinkFormatException($"Некорректный порт «{portText}»: нужен один порт 1–65535");
        }

        return (Decode(host), port);
    }
}
