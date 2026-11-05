using System.Security.Cryptography;

using FlowGate.Contracts.DomainLists;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Общие операции над загруженными файлами источников
/// </summary>
public static class DatFile
{
    private const int MaxEntryBytes = 64 * 1024 * 1024;

    /// <summary>
    ///     Считает SHA-256 файла
    /// </summary>
    /// <param name="path">Путь</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Хеш в hex нижнего регистра</returns>
    public static async Task<string> Sha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    /// <summary>
    ///     Перебирает записи верхнего уровня <c>.dat</c>
    /// </summary>
    /// <param name="path">Путь к файлу</param>
    /// <param name="visit">Обработчик записи (байты действительны только внутри вызова); вернуть false, чтобы остановиться</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="DomainListException">Если файла нет или он повреждён</exception>
    public static void ForEachEntry(string? path, Func<ReadOnlyMemory<byte>, bool> visit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(visit);

        if (path is null || !File.Exists(path))
        {
            throw new DomainListException("Источник ещё не загружен: обновите его");
        }

        try
        {
            using var stream = new BufferedStream(File.OpenRead(path), 64 * 1024);
            foreach (var entry in ProtoStream.ReadEntries(stream, MaxEntryBytes))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!visit(entry))
                {
                    return;
                }
            }
        }
        catch (IOException ex)
        {
            throw new DomainListException("Файл .dat повреждён или обрезан", ex);
        }
    }
}
