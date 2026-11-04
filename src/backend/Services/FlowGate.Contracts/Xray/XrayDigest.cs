using System.Globalization;

namespace FlowGate.Contracts.Xray;

/// <summary>
///     Контрольная сумма архива Xray из файла <c>.dgst</c> (строка <c>SHA2-256= &lt;hex&gt;</c>)
/// </summary>
public sealed class XrayDigest
{
    private const string Sha256Prefix = "SHA2-256=";

    private readonly byte[] _sha256;

    private XrayDigest(byte[] sha256)
    {
        _sha256 = sha256;
    }

    /// <summary>
    ///     SHA-256 в нижнем регистре hex
    /// </summary>
    public string Sha256Hex => Convert.ToHexStringLower(_sha256);

    /// <summary>
    ///     Разбирает содержимое файла <c>.dgst</c>
    /// </summary>
    /// <param name="content">Текст файла</param>
    /// <returns>Контрольная сумма</returns>
    /// <exception cref="FormatException">Если в файле нет корректной строки SHA2-256</exception>
    public static XrayDigest Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var line = content
            .Split('\n')
            .Select(item => item.Trim())
            .FirstOrDefault(item => item.StartsWith(Sha256Prefix, StringComparison.OrdinalIgnoreCase))
            ?? throw new FormatException("В .dgst нет строки SHA2-256");

        var hex = line[Sha256Prefix.Length..].Trim();
        if (hex.Length != 64 || !hex.All(char.IsAsciiHexDigit))
        {
            throw new FormatException($"Некорректный SHA2-256 в .dgst: «{hex}»");
        }

        return new XrayDigest(Convert.FromHexString(hex.ToLower(CultureInfo.InvariantCulture)));
    }

    /// <summary>
    ///     Сверяет посчитанный SHA-256 с ожидаемым
    /// </summary>
    /// <param name="actualSha256">Посчитанный хеш файла</param>
    /// <returns>true, если совпадает</returns>
    public bool Matches(ReadOnlySpan<byte> actualSha256) => actualSha256.SequenceEqual(_sha256);
}
