using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор <c>hysteria2://auth@host:port/?sni=…&amp;obfs=salamander&amp;obfs-password=…#name</c> (и <c>hy2://</c>).
///     В Xray 26 это outbound <c>hysteria</c> с транспортом <c>hysteria</c> и обязательным TLS
/// </summary>
public static class Hysteria2LinkParser
{
    /// <summary>
    ///     Разбирает ссылку Hysteria2
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер с outbound <c>hysteria</c></returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна или obfs не salamander</exception>
    public static ParsedServer Parse(string link)
    {
        var parts = ShareLink.Parse(link);
        if (parts.Path is not ("" or "/"))
        {
            throw new ServerLinkFormatException("В ссылке Hysteria2 после порта допускается только «/»");
        }

        var auth = ShareLink.Decode(parts.UserInfo);
        if (auth.Length == 0)
        {
            throw new ServerLinkFormatException("В ссылке Hysteria2 нет пароля (auth)");
        }

        var tls = new JsonObject();
        if (parts.Get("sni", "peer") is { } sni)
        {
            tls["serverName"] = sni;
        }

        if (parts.Get("alpn") is { } alpn)
        {
            tls["alpn"] = new JsonArray(alpn.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(item => (JsonNode?)item)
                .ToArray());
        }

        if (parts.Get("insecure", "allowInsecure") is "1" or "true")
        {
            tls["allowInsecure"] = true;
        }

        if (parts.Get("pinSHA256", "pcs") is { } pin)
        {
            tls["pinnedPeerCertSha256"] = pin;
        }

        var stream = new JsonObject
        {
            ["network"] = "hysteria",
            ["security"] = "tls",
            ["tlsSettings"] = tls,
            ["hysteriaSettings"] = new JsonObject { ["version"] = 2, ["auth"] = auth },
        };

        if (parts.Get("obfs") is { } obfs)
        {
            if (!string.Equals(obfs, "salamander", StringComparison.OrdinalIgnoreCase))
            {
                throw new ServerLinkFormatException($"Обфускация Hysteria2 «{obfs}» не поддерживается: нужна salamander");
            }

            var obfsPassword = parts.Get("obfs-password", "obfsPassword")
                ?? throw new ServerLinkFormatException("Для obfs=salamander нужен obfs-password");

            stream["finalmask"] = new JsonObject
            {
                ["udp"] = new JsonArray(new JsonObject
                {
                    ["type"] = "salamander",
                    ["settings"] = new JsonObject { ["password"] = obfsPassword },
                }),
            };
        }

        return new ParsedServer(
            parts.DisplayName,
            new JsonObject
            {
                ["protocol"] = "hysteria",
                ["settings"] = new JsonObject
                {
                    ["version"] = 2,
                    ["address"] = parts.Host,
                    ["port"] = parts.Port,
                },
                ["streamSettings"] = stream,
            });
    }
}
