using System.Text.Json.Serialization;

namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Откуда брать <c>.dat</c>: из последнего релиза GitHub-репозитория или по прямой ссылке
/// </summary>
/// <param name="ReleaseRepository">Репозиторий релизов <c>owner/repo</c> или null для прямой ссылки</param>
/// <param name="Asset">Имя файла в релизе, например <c>dlc.dat</c></param>
/// <param name="ChecksumAsset">Имя файла контрольной суммы в релизе или null</param>
/// <param name="Url">Прямая ссылка на файл или null для релиза. Может содержать токен — в логи только хост</param>
/// <param name="ChecksumUrl">Прямая ссылка на контрольную сумму SHA-256 или null</param>
public sealed record DatOrigin(
    string? ReleaseRepository,
    string? Asset,
    string? ChecksumAsset,
    string? Url,
    string? ChecksumUrl)
{
    /// <summary>
    ///     Файл берётся по прямой ссылке
    /// </summary>
    [JsonIgnore]
    public bool IsDirectUrl => Url is not null;

    /// <summary>
    ///     Создаёт источник из релиза GitHub
    /// </summary>
    /// <param name="repository">Репозиторий <c>owner/repo</c></param>
    /// <param name="asset">Имя файла</param>
    /// <param name="checksumAsset">Имя файла контрольной суммы или null</param>
    /// <returns>Описание источника</returns>
    public static DatOrigin FromRelease(string repository, string asset, string? checksumAsset) =>
        new(repository, asset, checksumAsset, null, null);

    /// <summary>
    ///     Создаёт источник по прямой ссылке
    /// </summary>
    /// <param name="url">Ссылка на файл</param>
    /// <param name="checksumUrl">Ссылка на контрольную сумму или null</param>
    /// <returns>Описание источника</returns>
    public static DatOrigin FromUrl(string url, string? checksumUrl) => new(null, null, null, url, checksumUrl);
}
