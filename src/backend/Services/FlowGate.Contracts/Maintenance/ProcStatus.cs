using System.Globalization;

namespace FlowGate.Contracts.Maintenance;

/// <summary>
///     Разбор <c>/proc/&lt;pid&gt;/status</c> Linux
/// </summary>
public static class ProcStatus
{
    private const string RssField = "VmRSS:";

    /// <summary>
    ///     Достаёт резидентную память процесса из строки <c>VmRSS:   30512 kB</c>
    /// </summary>
    /// <param name="status">Содержимое файла status</param>
    /// <returns>КиБ или null, если поля нет</returns>
    public static long? ParseRssKilobytes(string? status)
    {
        var line = status?.Split('\n').FirstOrDefault(line => line.StartsWith(RssField, StringComparison.Ordinal));
        var value = line?[RssField.Length..].Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var kilobytes) ? kilobytes : null;
    }
}
