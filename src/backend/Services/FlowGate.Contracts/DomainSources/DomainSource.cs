using System.Text.Json.Serialization;

namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Источник списков доменов и подсетей
/// </summary>
/// <param name="Id">Идентификатор; для встроенных — <c>v2fly</c> и <c>v2fly-geoip</c></param>
/// <param name="Name">Имя для пользователя</param>
/// <param name="Kind">Вид источника</param>
/// <param name="Repository">Для <see cref="DomainSourceKind.GitHubRepository"/>: <c>owner/repo</c></param>
/// <param name="Branch">Для репозитория: ветка или null — ветка по умолчанию</param>
/// <param name="Dat">Для <c>.dat</c>: откуда брать файл</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
/// <param name="BuiltIn">Встроенный источник: удалить нельзя</param>
/// <param name="Downloaded">Версия загруженного файла или null, если ещё не загружен</param>
/// <param name="LastRefresh">Итог последнего обновления или null</param>
/// <param name="CreatedAt">Когда добавлен</param>
public sealed record DomainSource(
    string Id,
    string Name,
    DomainSourceKind Kind,
    string? Repository,
    string? Branch,
    DatOrigin? Dat,
    bool AutoUpdate,
    bool BuiltIn,
    DomainSourceRevision? Downloaded,
    DomainSourceRefresh? LastRefresh,
    DateTimeOffset CreatedAt)
{
    /// <summary>
    ///     Идентификатор встроенного v2fly geosite
    /// </summary>
    public const string V2flyGeositeId = "v2fly";

    /// <summary>
    ///     Идентификатор встроенного v2fly geoip
    /// </summary>
    public const string V2flyGeoipId = "v2fly-geoip";

    /// <summary>
    ///     Встроенные источники, которые есть с первого запуска (файлы качаются по запросу)
    /// </summary>
    public static IReadOnlyList<DomainSource> BuiltInSources { get; } =
    [
        new(
            V2flyGeositeId,
            "v2fly domain-list-community",
            DomainSourceKind.GeositeDat,
            null,
            null,
            DatOrigin.FromRelease("v2fly/domain-list-community", "dlc.dat", "dlc.dat.sha256sum"),
            AutoUpdate: true,
            BuiltIn: true,
            null,
            null,
            DateTimeOffset.UnixEpoch),
        new(
            V2flyGeoipId,
            "v2fly geoip",
            DomainSourceKind.GeoipDat,
            null,
            null,
            DatOrigin.FromRelease("v2fly/geoip", "geoip.dat", "geoip.dat.sha256sum"),
            AutoUpdate: true,
            BuiltIn: true,
            null,
            null,
            DateTimeOffset.UnixEpoch),
    ];

    /// <summary>
    ///     Хранит ли источник списки в файле (всё, кроме локального)
    /// </summary>
    [JsonIgnore]
    public bool HasFile => Kind != DomainSourceKind.Local;

    /// <summary>
    ///     Описание для логов без секретов: для прямой ссылки — только хост
    /// </summary>
    [JsonIgnore]
    public string SafeOrigin => Kind switch
    {
        DomainSourceKind.GitHubRepository => $"github.com/{Repository}",
        DomainSourceKind.Local => "локальный",
        _ when Dat is { IsDirectUrl: true } => Uri.TryCreate(Dat.Url, UriKind.Absolute, out var uri) ? uri.Host : "ссылка",
        _ => $"github.com/{Dat?.ReleaseRepository}",
    };
}
