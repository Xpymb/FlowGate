using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace FlowGate.Tests.Common.DomainSources;

/// <summary>
///     Сборка tar.gz в формате tarball GitHub (<c>&lt;owner&gt;-&lt;repo&gt;-&lt;sha&gt;/путь</c>) для тестов
/// </summary>
public static class RepositoryArchive
{
    /// <summary>
    ///     Собирает архив
    /// </summary>
    /// <param name="root">Корневая папка, как у GitHub</param>
    /// <param name="files">Файлы: путь от корня репозитория и текст</param>
    /// <returns>Содержимое tar.gz</returns>
    public static byte[] Create(string root, IReadOnlyDictionary<string, string> files)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        using (var tar = new TarWriter(gzip, TarEntryFormat.Pax, leaveOpen: false))
        {
            tar.WriteEntry(new PaxTarEntry(TarEntryType.Directory, root + "/"));
            foreach (var (path, text) in files)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, $"{root}/{path}")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(text)),
                };
                tar.WriteEntry(entry);
            }
        }

        return output.ToArray();
    }
}
