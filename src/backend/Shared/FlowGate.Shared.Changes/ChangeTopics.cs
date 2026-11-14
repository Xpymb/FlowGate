namespace FlowGate.Shared.Changes;

/// <summary>
///     Темы изменений, которые не соответствуют JSON-файлу данных. Темы файлов — имя файла без расширения
///     (<c>dns-routes</c>, <c>connections</c>, …)
/// </summary>
public static class ChangeTopics
{
    /// <summary>
    ///     Фоновая задача запущена, продвинулась или завершилась
    /// </summary>
    public const string Jobs = "jobs";

    /// <summary>
    ///     Изменилось состояние процесса Xray
    /// </summary>
    public const string Xray = "xray";

    /// <summary>
    ///     Тема хранилища JSON-файла
    /// </summary>
    /// <param name="fileName">Имя файла, например <c>dns-routes.json</c></param>
    /// <returns>Имя файла без расширения</returns>
    /// <exception cref="ArgumentException">Если fileName пустой</exception>
    public static string ForFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return Path.GetFileNameWithoutExtension(fileName);
    }
}
