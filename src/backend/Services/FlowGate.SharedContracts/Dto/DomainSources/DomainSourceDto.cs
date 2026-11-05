namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Источник списков
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя</param>
/// <param name="Kind"><c>GitHubRepository</c>, <c>GeositeDat</c>, <c>GeoipDat</c> или <c>Local</c></param>
/// <param name="Repository">Для репозитория: <c>owner/repo</c></param>
/// <param name="Branch">Для репозитория: ветка или null — по умолчанию</param>
/// <param name="ReleaseRepository">Для .dat из релиза: <c>owner/repo</c></param>
/// <param name="Asset">Для .dat из релиза: имя файла</param>
/// <param name="ChecksumAsset">Для .dat из релиза: файл контрольной суммы</param>
/// <param name="Url">Для .dat по ссылке: адрес (может содержать токен)</param>
/// <param name="ChecksumUrl">Для .dat по ссылке: адрес контрольной суммы</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
/// <param name="BuiltIn">Встроенный, удалить нельзя</param>
/// <param name="Downloaded">Файл загружен (для локального — всегда true)</param>
/// <param name="Revision">Версия: sha коммита, тег релиза или null</param>
/// <param name="ListCount">Списков в каталоге</param>
/// <param name="RouteCount">Маршрутов, которые используют источник</param>
/// <param name="LastRefresh">Итог последнего обновления или null</param>
public sealed record DomainSourceDto(
    string Id,
    string Name,
    string Kind,
    string? Repository,
    string? Branch,
    string? ReleaseRepository,
    string? Asset,
    string? ChecksumAsset,
    string? Url,
    string? ChecksumUrl,
    bool AutoUpdate,
    bool BuiltIn,
    bool Downloaded,
    string? Revision,
    int ListCount,
    int RouteCount,
    DomainSourceRefreshDto? LastRefresh);
