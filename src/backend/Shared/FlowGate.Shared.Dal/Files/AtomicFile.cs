namespace FlowGate.Shared.Dal.Files;

/// <summary>
///     Атомарная запись файлов: во временный файл рядом, затем переименованием поверх. На Linux права 600
/// </summary>
public static class AtomicFile
{
    /// <summary>
    ///     Атомарно записывает файл
    /// </summary>
    /// <param name="path">Полный путь к файлу</param>
    /// <param name="write">Пишет содержимое в поток временного файла</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public static async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);

        var temporaryPath = await WriteTemporaryAsync(path, write, cancellationToken);
        try
        {
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            DeleteIfExists(temporaryPath);
        }
    }

    /// <summary>
    ///     Атомарно записывает байты в файл
    /// </summary>
    /// <param name="path">Полный путь к файлу</param>
    /// <param name="content">Содержимое</param>
    /// <param name="cancellationToken">Токен отмены</param>
    public static Task WriteAllBytesAsync(string path, ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        WriteAsync(path, (stream, token) => stream.WriteAsync(content, token).AsTask(), cancellationToken);

    /// <summary>
    ///     Пишет временный файл рядом с <paramref name="path"/>, не заменяя сам файл. Нужен, когда содержимое
    ///     нужно проверить до замены
    /// </summary>
    /// <param name="path">Путь к будущему файлу</param>
    /// <param name="write">Пишет содержимое в поток временного файла</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к временному файлу в том же каталоге, с тем же расширением</returns>
    public static async Task<string> WriteTemporaryAsync(
        string path,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(write);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(directory);

        var temporaryPath = Path.Combine(directory, $".{Guid.NewGuid():N}.{Path.GetFileName(path)}");
        try
        {
            await using var stream = new FileStream(temporaryPath, CreateOptions());
            await write(stream, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true);
        }
        catch
        {
            DeleteIfExists(temporaryPath);
            throw;
        }

        return temporaryPath;
    }

    /// <summary>
    ///     Удаляет файл, если он есть
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    public static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static FileStreamOptions CreateOptions()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }
}
