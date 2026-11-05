using System.Text.Json;
using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Implementations.Servers.Parsing;

/// <summary>
///     Импорт серверов из Xray-JSON: полный конфиг (профиль), массив профилей, один outbound или массив outbound.
///     Берутся только исходящие на серверы; маршрутизация, DNS и балансировщики профиля не применяются — этим управляет Keenetic
/// </summary>
public static class XrayJsonImporter
{
    /// <summary>
    ///     Протоколы outbound, которые становятся серверами
    /// </summary>
    public static IReadOnlySet<string> SupportedProtocols { get; } =
        new HashSet<string>(StringComparer.Ordinal) { "vless", "vmess", "trojan", "shadowsocks", "hysteria", "wireguard", "socks", "http" };

    private static readonly HashSet<string> ServiceProtocols = new(StringComparer.Ordinal) { "freedom", "blackhole", "dns", "loopback" };

    private static readonly string[] IgnoredSections = ["routing", "dns", "observatory", "burstObservatory", "balancers", "inbounds", "fakedns", "policy"];

    /// <summary>
    ///     Импортирует серверы
    /// </summary>
    /// <param name="root">Разобранный JSON</param>
    /// <param name="bodyMetadata">Метаданные провайдера, найденные рядом с JSON</param>
    /// <returns>Серверы, отклонённые узлы и предупреждения</returns>
    /// <exception cref="ArgumentNullException">Если root равен null</exception>
    public static ServerImportBatch Import(JsonNode root, IReadOnlyDictionary<string, string>? bodyMetadata = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var servers = new List<ParsedServer>();
        var rejected = new List<ServerImportRejection>();
        var ignored = new SortedSet<string>(StringComparer.Ordinal);
        var index = 0;

        IEnumerable<JsonNode?> items = root is JsonArray array ? array : [root];
        foreach (var item in items)
        {
            if (item is not JsonObject node)
            {
                rejected.Add(new ServerImportRejection(++index, "Элемент JSON не является объектом"));
                continue;
            }

            if (node["outbounds"] is JsonArray outbounds)
            {
                foreach (var section in IgnoredSections.Where(node.ContainsKey))
                {
                    ignored.Add(section);
                }

                var profileServers = new List<ParsedServer>();
                foreach (var outbound in outbounds)
                {
                    ImportOutbound(outbound, Text(node, "remarks"), profileServers, rejected, ref index);
                }

                servers.AddRange(NumberWithinProfile(profileServers));
            }
            else if (node.ContainsKey("protocol"))
            {
                ImportOutbound(node, Text(node, "remarks"), servers, rejected, ref index);
            }
            else
            {
                rejected.Add(new ServerImportRejection(++index, "Неизвестный формат JSON: нет ни outbounds, ни protocol"));
            }
        }

        var warnings = ignored.Count == 0
            ? new List<string>()
            : [$"Разделы профиля не применяются ({string.Join(", ", ignored)}): маршрутизацией управляет Keenetic"];

        return new ServerImportBatch(
            servers,
            rejected,
            warnings,
            bodyMetadata ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Пробует разобрать текст как JSON-объект или массив
    /// </summary>
    /// <param name="text">Текст</param>
    /// <param name="json">Разобранный JSON</param>
    /// <returns>true, если текст — JSON-объект или массив</returns>
    public static bool TryParseJson(string text, out JsonNode? json)
    {
        json = null;
        var trimmed = text.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is not ('{' or '['))
        {
            return false;
        }

        try
        {
            json = JsonNode.Parse(trimmed, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return json is JsonObject or JsonArray;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ImportOutbound(
        JsonNode? node,
        string remarks,
        List<ParsedServer> target,
        List<ServerImportRejection> rejected,
        ref int index)
    {
        if (node is not JsonObject outbound)
        {
            rejected.Add(new ServerImportRejection(++index, "Outbound не является объектом"));
            return;
        }

        var protocol = Text(outbound, "protocol").ToLowerInvariant();
        if (ServiceProtocols.Contains(protocol))
        {
            return;
        }

        index++;
        if (!SupportedProtocols.Contains(protocol))
        {
            rejected.Add(new ServerImportRejection(index, $"Протокол outbound «{protocol}» не поддерживается"));
            return;
        }

        if (IsChained(outbound))
        {
            rejected.Add(new ServerImportRejection(index, "Цепочки прокси (proxySettings, dialerProxy) не поддерживаются"));
            return;
        }

        var copy = (JsonObject)outbound.DeepClone();
        var tag = Text(copy, "tag");
        copy.Remove("tag");

        var summary = OutboundSummary.From(copy);
        var name = remarks.Length > 0 ? remarks
            : tag.Length > 0 ? tag
            : summary.Port is { } port ? $"{summary.Address}:{port}"
            : protocol;

        target.Add(new ParsedServer(name, copy));
    }

    private static IEnumerable<ParsedServer> NumberWithinProfile(List<ParsedServer> servers) =>
        servers.Count <= 1
            ? servers
            : servers.Select((server, position) => server with { Name = $"{server.Name} #{position + 1}" });

    /// <summary>
    ///     Outbound цепочки: ходит через другой outbound
    /// </summary>
    /// <param name="outbound">Outbound</param>
    /// <returns>true, если есть proxySettings или sockopt.dialerProxy</returns>
    public static bool IsChained(JsonObject outbound)
    {
        ArgumentNullException.ThrowIfNull(outbound);

        return outbound.ContainsKey("proxySettings")
            || (outbound["streamSettings"] as JsonObject)?["sockopt"] is JsonObject sockopt && sockopt.ContainsKey("dialerProxy");
    }

    private static string Text(JsonObject node, string name) =>
        node[name] is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>().Trim() : string.Empty;
}
