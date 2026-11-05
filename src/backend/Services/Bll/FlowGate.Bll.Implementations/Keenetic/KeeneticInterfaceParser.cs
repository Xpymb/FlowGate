using System.Text.Json;

using FlowGate.Contracts.Keenetic;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Разбор ответа RCI <c>show/interface</c>: объект, где ключ — имя интерфейса
/// </summary>
public static class KeeneticInterfaceParser
{
    private const long NeverHandshake = int.MaxValue;

    /// <summary>
    ///     Разбирает все интерфейсы
    /// </summary>
    /// <param name="interfaces">Ответ <c>show/interface</c></param>
    /// <returns>Состояния по имени интерфейса (без учёта регистра)</returns>
    /// <exception cref="KeeneticUnavailableException">Если ответ не объект</exception>
    public static IReadOnlyDictionary<string, KeeneticInterfaceState> ParseAll(JsonElement interfaces)
    {
        if (interfaces.ValueKind != JsonValueKind.Object)
        {
            throw new KeeneticUnavailableException("RCI show/interface вернул не объект");
        }

        return interfaces.EnumerateObject()
            .Where(property => property.Value.ValueKind == JsonValueKind.Object)
            .Select(property => Parse(property.Name, property.Value))
            .ToDictionary(state => state.Name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Разбирает один интерфейс
    /// </summary>
    /// <param name="name">Имя интерфейса (ключ в ответе)</param>
    /// <param name="element">Объект интерфейса</param>
    /// <returns>Состояние</returns>
    public static KeeneticInterfaceState Parse(string name, JsonElement element)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var peers = element.TryGetProperty("wireguard", out var wireguard)
            && wireguard.ValueKind == JsonValueKind.Object
            && wireguard.TryGetProperty("peer", out var peerArray)
            && peerArray.ValueKind == JsonValueKind.Array
                ? peerArray.EnumerateArray().Where(peer => peer.ValueKind == JsonValueKind.Object).Select(ParsePeer).ToList()
                : [];

        return new KeeneticInterfaceState(
            name,
            ReadString(element, "type") ?? string.Empty,
            ReadString(element, "description") ?? string.Empty,
            ReadString(element, "state") ?? string.Empty,
            ReadString(element, "link") ?? string.Empty,
            string.Equals(ReadString(element, "connected"), "yes", StringComparison.OrdinalIgnoreCase),
            ReadString(element, "address"),
            ReadString(element, "remote-endpoint-address"),
            peers);
    }

    private static KeeneticWireguardPeer ParsePeer(JsonElement peer)
    {
        var handshake = ReadLong(peer, "last-handshake");
        return new KeeneticWireguardPeer(
            ReadString(peer, "public-key") ?? string.Empty,
            ReadString(peer, "remote-endpoint-address"),
            ReadLong(peer, "remote-port") is { } port ? (int)port : null,
            peer.TryGetProperty("online", out var online) && online.ValueKind == JsonValueKind.True,
            handshake is null or >= NeverHandshake or < 0 ? null : handshake);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static long? ReadLong(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number)
            ? number
            : null;
}
