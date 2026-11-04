using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Разбор <c>vless://uuid@host:port?type=…&amp;security=…#name</c>
/// </summary>
public static class VlessLinkParser
{
    /// <summary>
    ///     Разбирает ссылку VLESS
    /// </summary>
    /// <param name="link">Ссылка</param>
    /// <returns>Сервер с outbound <c>vless</c></returns>
    /// <exception cref="ServerLinkFormatException">Если ссылка некорректна</exception>
    public static ParsedServer Parse(string link)
    {
        var parts = ShareLink.Parse(link);
        var id = ShareLink.Decode(parts.UserInfo);
        if (id.Length == 0)
        {
            throw new ServerLinkFormatException("В ссылке VLESS нет UUID");
        }

        var settings = new JsonObject
        {
            ["address"] = parts.Host,
            ["port"] = parts.Port,
            ["id"] = id,
            ["encryption"] = parts.Get("encryption") ?? "none",
        };

        if (parts.Get("flow") is { } flow)
        {
            settings["flow"] = flow;
        }

        return new ParsedServer(
            parts.DisplayName,
            new JsonObject
            {
                ["protocol"] = "vless",
                ["settings"] = settings,
                ["streamSettings"] = StreamSettingsBuilder.Build(parts, "none"),
            });
    }
}
