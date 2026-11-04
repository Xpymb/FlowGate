namespace FlowGate.Tests.Common.Xray;

/// <summary>
///     Содержимое фейкового бинарника Xray: версия записана прямо в файл, её читает <see cref="Processes.FakeProcessRunner"/>
/// </summary>
public static class FakeXrayBinary
{
    private const string Prefix = "fake-xray-version:";

    /// <summary>
    ///     Содержимое бинарника указанной версии
    /// </summary>
    /// <param name="version">Версия, например <c>26.10.31</c></param>
    /// <returns>Текст файла</returns>
    public static string Content(string version) => Prefix + version;

    /// <summary>
    ///     Читает версию из фейкового бинарника
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <returns>Версия или null, если файла нет или это не фейковый бинарник</returns>
    public static string? ReadVersion(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var content = File.ReadAllText(path);
        return content.StartsWith(Prefix, StringComparison.Ordinal) ? content[Prefix.Length..].Trim() : null;
    }

    /// <summary>
    ///     Вывод <c>xray version</c>, как у настоящего Xray
    /// </summary>
    /// <param name="version">Версия</param>
    /// <returns>Текст вывода</returns>
    public static string VersionOutput(string version) =>
        $"Xray {version} (Xray, Penetrates Everything.) a4a3dff (go1.27.3 linux/arm64){Environment.NewLine}A unified platform for anti-censorship.{Environment.NewLine}";
}
