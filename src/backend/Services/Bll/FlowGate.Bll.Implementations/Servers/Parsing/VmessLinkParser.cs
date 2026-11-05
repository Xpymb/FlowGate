using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор <c>vmess://base64(JSON)</c> в формате v2rayN (<c>ps</c>, <c>add</c>, <c>port</c>, <c>id</c>, <c>scy</c>,
///     <c>net</c>, <c>type</c>, <c>host</c>, <c>path</c>, <c>tls</c>, <c>sni</c>, <c>alpn</c>, <c>fp</c>)
/// </summary>
public static class VmessLinkParser
{
    /// <summary>
    ///     Разбирает ссылку VMess
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер с outbound <c>vmess</c></returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна</exception>
    public static ParsedServer Parse(string link)
    {
        ArgumentNullException.ThrowIfNull(link);

        var body = link[(link.IndexOf("://", StringComparison.Ordinal) + 3)..].Trim();
        var hashIndex = body.IndexOf('#');
        if (hashIndex >= 0)
        {
            body = body[..hashIndex];
        }

        if (!ShareLink.TryDecodeBase64(body, out var bytes))
        {
            throw new ServerLinkFormatException("Ссылка VMess должна содержать base64 JSON (формат v2rayN)");
        }

        JsonObject json;
        try
        {
            json = JsonNode.Parse(Encoding.UTF8.GetString(bytes)) as JsonObject
                ?? throw new ServerLinkFormatException("Ссылка VMess должна содержать JSON-объект");
        }
        catch (JsonException)
        {
            throw new ServerLinkFormatException("Ссылка VMess содержит некорректный JSON");
        }

        var address = Text(json, "add", "address");
        var portText = Text(json, "port");
        var id = Text(json, "id");
        if (address.Length == 0 || id.Length == 0)
        {
            throw new ServerLinkFormatException("В ссылке VMess нет адреса или id");
        }

        if (!int.TryParse(portText, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port is < 1 or > 65535)
        {
            throw new ServerLinkFormatException($"Некорректный порт «{portText}» в ссылке VMess");
        }

        var network = Text(json, "net", "network");
        var query = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["type"] = network.Length == 0 ? "tcp" : network,
            ["security"] = Text(json, "tls", "security") is { Length: > 0 } tls ? tls : "none",
            ["headerType"] = Text(json, "type"),
            ["host"] = Text(json, "host"),
            ["path"] = Text(json, "path"),
            ["sni"] = Text(json, "sni"),
            ["alpn"] = Text(json, "alpn"),
            ["fp"] = Text(json, "fp"),
            ["pbk"] = Text(json, "pbk"),
            ["sid"] = Text(json, "sid"),
            ["spx"] = Text(json, "spx"),
        };

        if (StreamSettingsBuilder.NormalizeNetwork(query["type"]) == "grpc")
        {
            query["serviceName"] = query["path"];
            query["path"] = string.Empty;
            query["mode"] = query["headerType"] == "multi" ? "multi" : string.Empty;
        }

        var parts = new ShareLink("vmess", string.Empty, address, port, string.Empty, query, Text(json, "ps", "remarks"));
        var security = Text(json, "scy");

        return new ParsedServer(
            parts.DisplayName,
            new JsonObject
            {
                ["protocol"] = "vmess",
                ["settings"] = new JsonObject
                {
                    ["address"] = address,
                    ["port"] = port,
                    ["id"] = id,
                    ["security"] = security.Length == 0 ? "auto" : security,
                },
                ["streamSettings"] = StreamSettingsBuilder.Build(parts, "none"),
            });
    }

    private static string Text(JsonObject json, params string[] names)
    {
        foreach (var name in names)
        {
            if (json[name] is JsonValue value)
            {
                var text = value.GetValueKind() switch
                {
                    JsonValueKind.String => value.GetValue<string>(),
                    JsonValueKind.Number => value.ToJsonString(),
                    _ => string.Empty,
                };

                if (text.Length > 0)
                {
                    return text.Trim();
                }
            }
        }

        return string.Empty;
    }
}
