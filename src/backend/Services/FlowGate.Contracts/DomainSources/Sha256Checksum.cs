namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Разбор файла контрольной суммы в формате <c>sha256sum</c> (<c>&lt;hex&gt;  dlc.dat</c>) или просто hex
/// </summary>
public static class Sha256Checksum
{
    /// <summary>
    ///     Возвращает SHA-256 из файла контрольной суммы
    /// </summary>
    /// <param name="content">Содержимое файла</param>
    /// <returns>Хеш в нижнем регистре</returns>
    /// <exception cref="FormatException">Если в файле нет 64 hex-символов в начале строки</exception>
    public static string Parse(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var token = content
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(candidate => candidate.Length == 64 && candidate.All(char.IsAsciiHexDigit));

        return token?.ToLowerInvariant() ?? throw new FormatException("В файле контрольной суммы нет SHA-256");
    }
}
