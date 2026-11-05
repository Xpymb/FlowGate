namespace FlowGate.Contracts.Xray;

/// <summary>
///     Релиз Xray, пригодный для установки: есть архив под архитектуру роутера и его <c>.dgst</c>
/// </summary>
public sealed class XrayRelease
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayRelease"/>
    /// </summary>
    /// <param name="version">Версия</param>
    /// <param name="tag">Тег релиза</param>
    /// <param name="publishedAt">Дата публикации</param>
    /// <param name="isPrerelease">Предварительный релиз</param>
    /// <param name="archiveUrl">Адрес архива</param>
    /// <param name="archiveSize">Размер архива, байты</param>
    /// <param name="digestUrl">Адрес файла <c>.dgst</c></param>
    /// <exception cref="ArgumentNullException">Если version, tag, archiveUrl или digestUrl равны null</exception>
    public XrayRelease(
        XrayVersion version,
        string tag,
        DateTimeOffset? publishedAt,
        bool isPrerelease,
        Uri archiveUrl,
        long archiveSize,
        Uri digestUrl)
    {
        Version = version ?? throw new ArgumentNullException(nameof(version));
        Tag = tag ?? throw new ArgumentNullException(nameof(tag));
        PublishedAt = publishedAt;
        IsPrerelease = isPrerelease;
        ArchiveUrl = archiveUrl ?? throw new ArgumentNullException(nameof(archiveUrl));
        ArchiveSize = archiveSize;
        DigestUrl = digestUrl ?? throw new ArgumentNullException(nameof(digestUrl));
    }

    /// <summary>
    ///     Версия
    /// </summary>
    public XrayVersion Version { get; }

    /// <summary>
    ///     Тег релиза
    /// </summary>
    public string Tag { get; }

    /// <summary>
    ///     Дата публикации
    /// </summary>
    public DateTimeOffset? PublishedAt { get; }

    /// <summary>
    ///     Предварительный релиз
    /// </summary>
    public bool IsPrerelease { get; }

    /// <summary>
    ///     Адрес архива
    /// </summary>
    public Uri ArchiveUrl { get; }

    /// <summary>
    ///     Размер архива, байты
    /// </summary>
    public long ArchiveSize { get; }

    /// <summary>
    ///     Адрес файла <c>.dgst</c>
    /// </summary>
    public Uri DigestUrl { get; }

    /// <summary>
    ///     Создаёт релиз из данных GitHub, если в нём есть архив и его <c>.dgst</c> и тег разбирается как версия
    /// </summary>
    /// <param name="tag">Тег</param>
    /// <param name="publishedAt">Дата публикации</param>
    /// <param name="isPrerelease">Предварительный релиз</param>
    /// <param name="isDraft">Черновик — такие не устанавливаются</param>
    /// <param name="assets">Файлы релиза: имя, адрес, размер</param>
    /// <param name="archiveName">Имя нужного архива, например <c>Xray-linux-arm64-v8a.zip</c></param>
    /// <returns>Релиз или null, если он не подходит</returns>
    public static XrayRelease? TryCreate(
        string tag,
        DateTimeOffset? publishedAt,
        bool isPrerelease,
        bool isDraft,
        IReadOnlyCollection<(string Name, string Url, long Size)> assets,
        string archiveName)
    {
        ArgumentNullException.ThrowIfNull(assets);

        if (isDraft || !XrayVersion.TryParse(tag, out var version))
        {
            return null;
        }

        var archive = assets.FirstOrDefault(asset => asset.Name == archiveName);
        var digest = assets.FirstOrDefault(asset => asset.Name == archiveName + ".dgst");
        if (archive.Name is null || digest.Name is null
            || !Uri.TryCreate(archive.Url, UriKind.Absolute, out var archiveUrl)
            || !Uri.TryCreate(digest.Url, UriKind.Absolute, out var digestUrl))
        {
            return null;
        }

        return new XrayRelease(version!, tag, publishedAt, isPrerelease, archiveUrl, archive.Size, digestUrl);
    }
}
