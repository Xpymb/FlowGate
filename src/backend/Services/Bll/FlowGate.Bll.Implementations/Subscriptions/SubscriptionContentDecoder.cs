using System.Text;
using System.Text.Json.Nodes;

using FlowGate.Bll.Implementations.Servers.Parsing;
using FlowGate.Contracts.Subscriptions;

namespace FlowGate.Bll.Implementations.Subscriptions;

/// <summary>
///     Раскрытие содержимого подписки: JSON (проверяется первым), base64 или строки со ссылками.
///     Строки метаданных (<c>#profile-title: …</c>) собираются со всех слоёв; внутренний слой важнее внешнего
/// </summary>
public static class SubscriptionContentDecoder
{
    /// <summary>
    ///     Ключи метаданных провайдера, которые распознаются в теле
    /// </summary>
    public static IReadOnlySet<string> MetadataKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "profile-title",
        "subscription-userinfo",
        "profile-update-interval",
        "announce",
        "announce-url",
        "support-url",
        "profile-web-page-url",
    };

    /// <summary>
    ///     Раскрывает содержимое
    /// </summary>
    /// <param name="body">Тело ответа или вставленный текст</param>
    /// <returns>JSON или ссылки плюс метаданные</returns>
    /// <exception cref="ArgumentNullException">Если body равен null</exception>
    public static DecodedSubscriptionContent Decode(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var text = ExtractMetadata(body.TrimStart('﻿').Trim(), metadata);

        if (XrayJsonImporter.TryParseJson(text, out var json))
        {
            return new DecodedSubscriptionContent(json, [], metadata, 0);
        }

        if (!text.Contains("://", StringComparison.Ordinal) && ShareLink.TryDecodeBase64(text, out var bytes))
        {
            var decoded = ExtractMetadata(Encoding.UTF8.GetString(bytes).TrimStart('﻿').Trim(), metadata);
            if (XrayJsonImporter.TryParseJson(decoded, out json))
            {
                return new DecodedSubscriptionContent(json, [], metadata, 0);
            }

            text = decoded;
        }

        var links = new List<string>();
        var ignored = 0;
        foreach (var line in SplitLines(text))
        {
            if (line.Contains("://", StringComparison.Ordinal) && !line.StartsWith('#'))
            {
                links.Add(line);
            }
            else if (!line.StartsWith('#') && !line.StartsWith("//", StringComparison.Ordinal))
            {
                ignored++;
            }
        }

        return new DecodedSubscriptionContent(null, links, metadata, ignored);
    }

    private static string ExtractMetadata(string text, Dictionary<string, string> metadata)
    {
        var remaining = new StringBuilder(text.Length);
        foreach (var line in SplitLines(text))
        {
            if (TryParseMetadataLine(line, out var key, out var value))
            {
                metadata[key] = value;
            }
            else
            {
                remaining.Append(line).Append('\n');
            }
        }

        return remaining.ToString().Trim();
    }

    private static bool TryParseMetadataLine(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;

        var content = line.StartsWith('#') ? line.TrimStart('#').Trim() : line;
        var separator = content.IndexOfAny([':', '=']);
        if (separator <= 0)
        {
            return false;
        }

        var candidate = content[..separator].Trim();
        if (!MetadataKeys.Contains(candidate))
        {
            return false;
        }

        key = candidate.ToLowerInvariant();
        value = content[(separator + 1)..].Trim();
        return true;
    }

    private static IEnumerable<string> SplitLines(string text) =>
        text.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0);
}
