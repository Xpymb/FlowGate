using System.Text.Json;

using FlowGate.Contracts.DnsRoutes;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Разбор ответов RCI <c>object-group/fqdn</c> и <c>dns-proxy/route</c> (формат снят на KeeneticOS 5.2,
///     docs/keenetic-findings.md §100)
/// </summary>
public static class KeeneticDnsParser
{
    /// <summary>
    ///     Разбирает группы: <c>{"&lt;имя&gt;": {"description": "...", "include": [{"address": "..."}]}}</c>
    /// </summary>
    /// <param name="groups">Ответ <c>GET /rci/object-group/fqdn</c></param>
    /// <returns>Группы по имени</returns>
    /// <exception cref="KeeneticUnavailableException">Если ответ не объект</exception>
    public static IReadOnlyDictionary<string, NdmsGroup> ParseGroups(JsonElement groups)
    {
        if (groups.ValueKind != JsonValueKind.Object)
        {
            throw new KeeneticUnavailableException("RCI object-group/fqdn вернул не объект");
        }

        return groups.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Object)
            .Select(property => new NdmsGroup(
                property.Name,
                ReadString(property.Value, "description") ?? string.Empty,
                property.Value.TryGetProperty("include", out var include) && include.ValueKind == JsonValueKind.Array
                    ? include.EnumerateArray().Select(item => ReadString(item, "address")).OfType<string>().ToList()
                    : []))
            .ToDictionary(group => group.Name, StringComparer.Ordinal);
    }

    /// <summary>
    ///     Разбирает правила: <c>[{"group", "interface", "auto", "index", "comment", "disable"?}]</c>
    /// </summary>
    /// <param name="routes">Ответ <c>GET /rci/dns-proxy/route</c></param>
    /// <returns>Правила в порядке Keenetic</returns>
    /// <exception cref="KeeneticUnavailableException">Если ответ не массив</exception>
    public static IReadOnlyList<NdmsRoute> ParseRoutes(JsonElement routes)
    {
        if (routes.ValueKind != JsonValueKind.Array)
        {
            throw new KeeneticUnavailableException("RCI dns-proxy/route вернул не массив");
        }

        return routes.EnumerateArray()
            .Where(route => route.ValueKind == JsonValueKind.Object)
            .Select(route => (Group: ReadString(route, "group"), Interface: ReadString(route, "interface"), Route: route))
            .Where(item => item.Group is not null && item.Interface is not null)
            .Select(item => new NdmsRoute(item.Group!, item.Interface!, ReadBool(item.Route, "auto"), ReadBool(item.Route, "disable")))
            .ToList();
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
