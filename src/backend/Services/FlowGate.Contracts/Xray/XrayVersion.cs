using System.Globalization;

namespace FlowGate.Contracts.Xray;

/// <summary>
///     Версия Xray: числовые сегменты через точку (<c>26.10.31</c>, <c>1.8.24</c>)
/// </summary>
public sealed class XrayVersion : IComparable<XrayVersion>, IEquatable<XrayVersion>
{
    private readonly int[] _segments;

    private XrayVersion(int[] segments)
    {
        _segments = segments;
    }

    /// <summary>
    ///     Разбирает версию из тега (<c>v26.10.31</c>), числа (<c>26.10.31</c>) или вывода <c>xray version</c>
    ///     (<c>Xray 26.10.31 (Xray, Penetrates Everything.) …</c>)
    /// </summary>
    /// <param name="value">Строка с версией</param>
    /// <param name="version">Разобранная версия</param>
    /// <returns>true, если версию удалось разобрать</returns>
    public static bool TryParse(string? value, out XrayVersion? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var firstLine = value.Trim().Split('\n', 2)[0].Trim();
        var token = firstLine.StartsWith("Xray ", StringComparison.OrdinalIgnoreCase)
            ? firstLine["Xray ".Length..].Split(' ', 2)[0]
            : firstLine;
        token = token.TrimStart('v', 'V');

        var parts = token.Split('.');
        if (parts.Length is < 2 or > 4)
        {
            return false;
        }

        var segments = new int[parts.Length];
        for (var index = 0; index < parts.Length; index++)
        {
            if (!int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out segments[index]))
            {
                return false;
            }
        }

        version = new XrayVersion(segments);
        return true;
    }

    /// <summary>
    ///     Разбирает версию
    /// </summary>
    /// <param name="value">Строка с версией</param>
    /// <returns>Версия</returns>
    /// <exception cref="FormatException">Если версию не удалось разобрать</exception>
    public static XrayVersion Parse(string value) =>
        TryParse(value, out var version) ? version! : throw new FormatException($"Не удалось разобрать версию Xray: «{value}»");

    /// <summary>
    ///     Сравнивает версии
    /// </summary>
    /// <param name="left">Левая версия</param>
    /// <param name="right">Правая версия</param>
    /// <returns>true, если левая новее</returns>
    public static bool operator >(XrayVersion left, XrayVersion right) => Compare(left, right) > 0;

    /// <summary>
    ///     Сравнивает версии
    /// </summary>
    /// <param name="left">Левая версия</param>
    /// <param name="right">Правая версия</param>
    /// <returns>true, если левая старее</returns>
    public static bool operator <(XrayVersion left, XrayVersion right) => Compare(left, right) < 0;

    /// <summary>
    ///     Сравнивает версии на равенство
    /// </summary>
    /// <param name="left">Левая версия</param>
    /// <param name="right">Правая версия</param>
    /// <returns>true, если версии равны</returns>
    public static bool operator ==(XrayVersion? left, XrayVersion? right) => Equals(left, right);

    /// <summary>
    ///     Сравнивает версии на неравенство
    /// </summary>
    /// <param name="left">Левая версия</param>
    /// <param name="right">Правая версия</param>
    /// <returns>true, если версии различаются</returns>
    public static bool operator !=(XrayVersion? left, XrayVersion? right) => !Equals(left, right);

    /// <inheritdoc />
    public int CompareTo(XrayVersion? other) => other is null ? 1 : Compare(this, other);

    /// <inheritdoc />
    public bool Equals(XrayVersion? other) => other is not null && Compare(this, other) == 0;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is XrayVersion other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var segment in Enumerable.Reverse(_segments).SkipWhile(segment => segment == 0))
        {
            hash.Add(segment);
        }

        return hash.ToHashCode();
    }

    /// <inheritdoc />
    public override string ToString() => string.Join('.', _segments);

    private static int Compare(XrayVersion left, XrayVersion right)
    {
        var length = Math.Max(left._segments.Length, right._segments.Length);
        for (var index = 0; index < length; index++)
        {
            var leftSegment = index < left._segments.Length ? left._segments[index] : 0;
            var rightSegment = index < right._segments.Length ? right._segments[index] : 0;
            if (leftSegment != rightSegment)
            {
                return leftSegment.CompareTo(rightSegment);
            }
        }

        return 0;
    }
}
