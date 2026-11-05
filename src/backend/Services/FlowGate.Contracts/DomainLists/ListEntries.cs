using System.Net;
using System.Net.Sockets;

namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Нормализация элементов списка. Keenetic хранит элементы как есть, поэтому один и тот же адрес в разной записи
///     дал бы дубликаты и лишние команды: домены — в нижнем регистре без точки в конце, адреса — каноническая запись,
///     у подсети сброшены биты хоста, подсеть /32 или /128 — просто адрес
/// </summary>
public static class ListEntries
{
    private const int MaxDomainLength = 253;
    private const int MaxLabelLength = 63;

    /// <summary>
    ///     Разбирает и нормализует домен, адрес или подсеть
    /// </summary>
    /// <param name="value">Значение из списка</param>
    /// <param name="entry">Нормализованный элемент</param>
    /// <param name="kind">Вид элемента</param>
    /// <returns>true, если значение корректно</returns>
    public static bool TryNormalize(string? value, out string entry, out ListEntryKind kind)
    {
        entry = string.Empty;
        kind = ListEntryKind.Domain;

        var text = value?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (TryNormalizeAddress(text, out entry, out kind))
        {
            return true;
        }

        return TryNormalizeDomain(text, out entry);
    }

    /// <summary>
    ///     Определяет вид уже нормализованного элемента
    /// </summary>
    /// <param name="entry">Элемент</param>
    /// <returns>Вид элемента</returns>
    public static ListEntryKind KindOf(string entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Contains(':', StringComparison.Ordinal))
        {
            return ListEntryKind.Ipv6;
        }

        var address = entry.Split('/', 2)[0];
        return IPAddress.TryParse(address, out var parsed) && parsed.AddressFamily == AddressFamily.InterNetwork
            ? ListEntryKind.Ipv4
            : ListEntryKind.Domain;
    }

    /// <summary>
    ///     Возвращает элемент для адреса и длины префикса (как в geoip.dat)
    /// </summary>
    /// <param name="address">Адрес (4 или 16 байт)</param>
    /// <param name="prefixLength">Длина префикса</param>
    /// <param name="entry">Нормализованный элемент</param>
    /// <returns>true, если префикс подходит адресу</returns>
    public static bool TryFromNetwork(ReadOnlySpan<byte> address, int prefixLength, out string entry)
    {
        entry = string.Empty;
        if (address.Length is not (4 or 16) || prefixLength < 0 || prefixLength > address.Length * 8)
        {
            return false;
        }

        entry = Format(new IPAddress(address), prefixLength);
        return true;
    }

    private static bool TryNormalizeAddress(string text, out string entry, out ListEntryKind kind)
    {
        entry = string.Empty;
        kind = ListEntryKind.Domain;

        var parts = text.Split('/', 2);
        if (!IPAddress.TryParse(parts[0], out var address) || !LooksLikeAddress(parts[0]))
        {
            return false;
        }

        var bits = address.AddressFamily == AddressFamily.InterNetwork ? 32 : 128;
        var prefix = bits;
        if (parts.Length == 2 && (!int.TryParse(parts[1], out prefix) || prefix < 0 || prefix > bits))
        {
            return false;
        }

        kind = bits == 32 ? ListEntryKind.Ipv4 : ListEntryKind.Ipv6;
        entry = Format(address, prefix);
        return true;
    }

    private static bool LooksLikeAddress(string text) =>
        text.Contains(':', StringComparison.Ordinal)
        || (text.Count(character => character == '.') == 3 && text.All(character => char.IsAsciiDigit(character) || character == '.'));

    private static string Format(IPAddress address, int prefixLength)
    {
        var bytes = address.GetAddressBytes();
        var bits = bytes.Length * 8;
        for (var bit = prefixLength; bit < bits; bit++)
        {
            bytes[bit / 8] &= (byte)~(0x80 >> (bit % 8));
        }

        var network = new IPAddress(bytes).ToString();
        return prefixLength == bits ? network : $"{network}/{prefixLength}";
    }

    private static bool TryNormalizeDomain(string text, out string entry)
    {
        entry = text.TrimEnd('.').ToLowerInvariant();
        if (entry.Length is 0 or > MaxDomainLength)
        {
            return false;
        }

        foreach (var label in entry.Split('.'))
        {
            if (label.Length is 0 or > MaxLabelLength
                || label[0] == '-'
                || label[^1] == '-'
                || !label.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
