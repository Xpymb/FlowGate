using System.Globalization;
using System.Text;

using FlowGate.Bll.Implementations.Servers.Parsing;
using FlowGate.Contracts.Subscriptions;

namespace FlowGate.Bll.Implementations.Subscriptions;

/// <summary>
///     Разбор метаданных провайдера: HTTP-заголовки и строки тела, тело важнее.
///     Значения вида <c>base64:…</c> декодируются; некорректные поля пропускаются, а не выдумываются
/// </summary>
public static class ProviderMetadataParser
{
    private const int MaxTextLength = 2000;

    /// <summary>
    ///     Собирает метаданные
    /// </summary>
    /// <param name="headers">Заголовки ответа, ключи без учёта регистра</param>
    /// <param name="body">Метаданные из тела</param>
    /// <returns>Метаданные или null, если провайдер ничего не сообщил</returns>
    public static ProviderMetadata? Parse(IReadOnlyDictionary<string, string> headers, IReadOnlyDictionary<string, string> body)
    {
        ArgumentNullException.ThrowIfNull(headers);
        ArgumentNullException.ThrowIfNull(body);

        string? Value(string key) =>
            (body.TryGetValue(key, out var fromBody) ? DecodeValue(fromBody) : null) is { Length: > 0 } bodyValue ? bodyValue
            : (headers.TryGetValue(key, out var fromHeader) ? DecodeValue(fromHeader) : null) is { Length: > 0 } headerValue ? headerValue
            : null;

        var userInfo = ParseUserInfo(Value("subscription-userinfo"));
        var metadata = new ProviderMetadata(
            Limit(Value("profile-title")),
            userInfo.GetValueOrDefault("upload"),
            userInfo.GetValueOrDefault("download"),
            userInfo.GetValueOrDefault("total") is long total and > 0 ? total : null,
            userInfo.GetValueOrDefault("expire") is long expire and > 0 ? DateTimeOffset.FromUnixTimeSeconds(expire) : null,
            ParseInterval(Value("profile-update-interval")),
            Limit(Value("announce")),
            ParseUrl(Value("announce-url")),
            ParseUrl(Value("support-url")),
            ParseUrl(Value("profile-web-page-url")));

        return metadata.IsEmpty ? null : metadata;
    }

    private static string DecodeValue(string value)
    {
        var trimmed = value.Trim();
        if (!trimmed.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var encoded = trimmed["base64:".Length..];
        return encoded.Length == 0 ? string.Empty
            : ShareLink.TryDecodeBase64(encoded, out var bytes) ? Encoding.UTF8.GetString(bytes).Trim()
            : trimmed;
    }

    private static Dictionary<string, long?> ParseUserInfo(string? value)
    {
        var result = new Dictionary<string, long?>(StringComparer.OrdinalIgnoreCase);
        if (value is null)
        {
            return result;
        }

        foreach (var part in value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            if (long.TryParse(part[(separator + 1)..].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number))
            {
                result[key] = number;
            }
        }

        return result;
    }

    private static int? ParseInterval(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var hours) && hours is >= 1 and <= 168 ? hours : null;

    private static string? ParseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.ToString()
            : null;

    private static string? Limit(string? value) =>
        value is null ? null : value.Length > MaxTextLength ? value[..MaxTextLength] : value;
}
