using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор <c>trojan://password@host:port?security=tls&amp;sni=…#name</c>
/// </summary>
public static class TrojanLinkParser
{
    /// <summary>
    ///     Разбирает ссылку Trojan; без <c>security</c> используется TLS
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер с outbound <c>trojan</c></returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна</exception>
    public static ParsedServer Parse(string link)
    {
        var parts = ShareLink.Parse(link);
        var password = ShareLink.Decode(parts.UserInfo);
        if (password.Length == 0)
        {
            throw new ServerLinkFormatException("В ссылке Trojan нет пароля");
        }

        return new ParsedServer(
            parts.DisplayName,
            new JsonObject
            {
                ["protocol"] = "trojan",
                ["settings"] = new JsonObject
                {
                    ["address"] = parts.Host,
                    ["port"] = parts.Port,
                    ["password"] = password,
                },
                ["streamSettings"] = StreamSettingsBuilder.Build(parts, "tls"),
            });
    }
}
