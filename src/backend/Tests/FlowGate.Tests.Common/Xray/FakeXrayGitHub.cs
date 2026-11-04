using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Common.Xray;

/// <summary>
///     Подменный GitHub для установки Xray: список релизов — настоящая фикстура, архивы и <c>.dgst</c> генерируются
///     (в архиве фейковый бинарник своей версии)
/// </summary>
public sealed partial class FakeXrayGitHub
{
    /// <summary>
    ///     Базовый адрес API
    /// </summary>
    public static readonly Uri ApiBaseAddress = new("https://api.github.com/");

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FakeXrayGitHub"/>
    /// </summary>
    public FakeXrayGitHub()
    {
        Handler = new FakeHttpMessageHandler(Respond);
    }

    /// <summary>
    ///     HTTP-обработчик для клиента GitHub
    /// </summary>
    public FakeHttpMessageHandler Handler { get; }

    /// <summary>
    ///     Содержимое бинарника в архиве версии; по умолчанию — фейковый Xray этой версии
    /// </summary>
    public Func<string, string> BinaryContent { get; set; } = FakeXrayBinary.Content;

    /// <summary>
    ///     Подмена текста <c>.dgst</c> для версии; null — правильный
    /// </summary>
    public Func<string, string?> DigestOverride { get; set; } = _ => null;

    /// <summary>
    ///     Если задан, скачивание архива ждёт этого события (чтобы удержать установку в процессе)
    /// </summary>
    public ManualResetEventSlim? DownloadGate { get; set; }

    /// <summary>
    ///     Собирает zip как у релиза Xray: <c>xray</c>, <c>geoip.dat</c>, <c>LICENSE</c>
    /// </summary>
    /// <param name="binaryContent">Содержимое файла <c>xray</c></param>
    /// <returns>Байты архива</returns>
    public static byte[] CreateArchive(string binaryContent)
    {
        using var buffer = new MemoryStream();
        using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "LICENSE", "MPL-2.0");
            AddEntry(zip, "geoip.dat", "geoip");
            AddEntry(zip, "xray", binaryContent);
        }

        return buffer.ToArray();
    }

    /// <summary>
    ///     Формирует <c>.dgst</c> как у релиза Xray
    /// </summary>
    /// <param name="archive">Байты архива</param>
    /// <returns>Текст файла</returns>
    public static string CreateDigest(byte[] archive) =>
        $"MD5= {Convert.ToHexStringLower(MD5.HashData(archive))}\nSHA2-256= {Convert.ToHexStringLower(SHA256.HashData(archive))}\n";

    private HttpResponseMessage Respond(RecordedRequest request)
    {
        var path = request.Uri?.AbsolutePath ?? string.Empty;

        if (path.EndsWith("/releases", StringComparison.Ordinal))
        {
            return FakeHttpMessageHandler.Json(GitHubFixtures.XrayReleases);
        }

        var match = DownloadPattern().Match(path);
        if (!match.Success)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        var version = match.Groups["version"].Value;
        var archive = CreateArchive(BinaryContent(version));

        if (match.Groups["digest"].Success)
        {
            return FakeHttpMessageHandler.Bytes(Encoding.UTF8.GetBytes(DigestOverride(version) ?? CreateDigest(archive)));
        }

        DownloadGate?.Wait(TimeSpan.FromSeconds(30));
        return FakeHttpMessageHandler.Bytes(archive);
    }

    private static void AddEntry(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    [GeneratedRegex(@"/download/v(?<version>[0-9.]+)/Xray-linux-arm64-v8a\.zip(?<digest>\.dgst)?$")]
    private static partial Regex DownloadPattern();
}
