using System.Text.Json;
using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Сборка <c>streamSettings</c> Xray из параметров ссылки (<c>type</c>, <c>security</c>, <c>sni</c>, <c>pbk</c> …)
/// </summary>
public static class StreamSettingsBuilder
{
    /// <summary>
    ///     Собирает <c>streamSettings</c>
    /// </summary>
    /// <param name="link">Ссылка с параметрами</param>
    /// <param name="defaultSecurity">Защита, если в ссылке нет <c>security</c> (<c>none</c> для vless, <c>tls</c> для trojan)</param>
    /// <returns>Объект <c>streamSettings</c></returns>
    /// <exception cref="ServerLinkFormatException">Если транспорт или защита не поддерживаются или не хватает параметров</exception>
    public static JsonObject Build(ShareLink link, string defaultSecurity)
    {
        ArgumentNullException.ThrowIfNull(link);

        var network = NormalizeNetwork(link.Get("type", "net") ?? "tcp");
        var security = (link.Get("security") ?? defaultSecurity).ToLowerInvariant();

        var stream = new JsonObject
        {
            ["network"] = network,
            ["security"] = security,
        };

        AddTransport(stream, network, link);
        AddSecurity(stream, security, link);
        return stream;
    }

    /// <summary>
    ///     Приводит имя транспорта к принятому в Xray 26
    /// </summary>
    /// <param name="network">Транспорт из ссылки</param>
    /// <returns>Имя транспорта Xray</returns>
    /// <exception cref="ServerLinkFormatException">Если транспорт не поддерживается</exception>
    public static string NormalizeNetwork(string network) =>
        network.ToLowerInvariant() switch
        {
            "tcp" or "raw" => "raw",
            "ws" or "websocket" => "ws",
            "grpc" or "gun" => "grpc",
            "httpupgrade" => "httpupgrade",
            "xhttp" or "splithttp" => "xhttp",
            var other => throw new ServerLinkFormatException($"Транспорт «{other}» не поддерживается"),
        };

    private static void AddTransport(JsonObject stream, string network, ShareLink link)
    {
        var path = link.Get("path");
        var host = link.Get("host");

        switch (network)
        {
            case "raw":
                {
                    if (string.Equals(link.Get("headerType"), "http", StringComparison.OrdinalIgnoreCase))
                    {
                        var request = new JsonObject { ["path"] = new JsonArray(path ?? "/") };
                        if (host is not null)
                        {
                            request["headers"] = new JsonObject { ["Host"] = SplitList(host) };
                        }

                        stream["rawSettings"] = new JsonObject
                        {
                            ["header"] = new JsonObject { ["type"] = "http", ["request"] = request },
                        };
                    }

                    break;
                }

            case "ws":
                {
                    stream["wsSettings"] = WithHost(new JsonObject { ["path"] = path ?? "/" }, host);
                    break;
                }

            case "httpupgrade":
                {
                    stream["httpupgradeSettings"] = WithHost(new JsonObject { ["path"] = path ?? "/" }, host);
                    break;
                }

            case "grpc":
                {
                    var grpc = new JsonObject { ["serviceName"] = link.Get("serviceName", "service_name") ?? string.Empty };
                    if (link.Get("authority") is { } authority)
                    {
                        grpc["authority"] = authority;
                    }

                    if (string.Equals(link.Get("mode"), "multi", StringComparison.OrdinalIgnoreCase))
                    {
                        grpc["multiMode"] = true;
                    }

                    stream["grpcSettings"] = grpc;
                    break;
                }

            case "xhttp":
                {
                    var xhttp = WithHost(new JsonObject { ["path"] = path ?? "/", ["mode"] = link.Get("mode") ?? "auto" }, host);
                    if (link.Get("extra") is { } extra)
                    {
                        xhttp["extra"] = ParseExtra(extra);
                    }

                    stream["xhttpSettings"] = xhttp;
                    break;
                }
        }
    }

    private static void AddSecurity(JsonObject stream, string security, ShareLink link)
    {
        switch (security)
        {
            case "none":
                {
                    break;
                }

            case "tls":
                {
                    var tls = new JsonObject();
                    SetIfPresent(tls, "serverName", link.Get("sni", "peer"));
                    SetIfPresent(tls, "fingerprint", link.Get("fp"));
                    if (link.Get("alpn") is { } alpn)
                    {
                        tls["alpn"] = SplitList(alpn);
                    }

                    if (IsTrue(link.Get("allowInsecure", "insecure")))
                    {
                        tls["allowInsecure"] = true;
                    }

                    SetIfPresent(tls, "pinnedPeerCertSha256", link.Get("pcs"));
                    SetIfPresent(tls, "echConfigList", link.Get("ech"));
                    stream["tlsSettings"] = tls;
                    break;
                }

            case "reality":
                {
                    var publicKey = link.Get("pbk", "publicKey", "password")
                        ?? throw new ServerLinkFormatException("Для REALITY в ссылке нужен параметр pbk (публичный ключ)");

                    var reality = new JsonObject();
                    SetIfPresent(reality, "serverName", link.Get("sni"));
                    reality["fingerprint"] = link.Get("fp") ?? "chrome";
                    reality["publicKey"] = publicKey;
                    SetIfPresent(reality, "shortId", link.Get("sid"));
                    SetIfPresent(reality, "spiderX", link.Get("spx"));
                    SetIfPresent(reality, "mldsa65Verify", link.Get("pqv"));
                    stream["realitySettings"] = reality;
                    break;
                }

            default:
                {
                    throw new ServerLinkFormatException($"Защита «{security}» не поддерживается: нужна none, tls или reality");
                }
        }
    }

    private static JsonObject ParseExtra(string extra)
    {
        try
        {
            return JsonNode.Parse(extra) as JsonObject
                ?? throw new ServerLinkFormatException("Параметр extra должен быть JSON-объектом");
        }
        catch (JsonException)
        {
            throw new ServerLinkFormatException("Параметр extra — некорректный JSON");
        }
    }

    private static JsonObject WithHost(JsonObject settings, string? host)
    {
        if (host is not null)
        {
            settings["host"] = host;
        }

        return settings;
    }

    private static JsonArray SplitList(string value) =>
        new(value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => (JsonNode?)item)
            .ToArray());

    private static void SetIfPresent(JsonObject target, string name, string? value)
    {
        if (value is not null)
        {
            target[name] = value;
        }
    }

    private static bool IsTrue(string? value) => value is "1" or "true" or "True" or "TRUE";
}
