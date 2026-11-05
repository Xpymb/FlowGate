namespace FlowGate.SharedContracts.Dto.DomainSources;

/// <summary>
///     Создание или изменение источника. Вид после создания не меняется; поля, которые к виду не относятся, игнорируются
/// </summary>
/// <param name="Name">Имя</param>
/// <param name="Kind"><c>GitHubRepository</c>, <c>GeositeDat</c>, <c>GeoipDat</c> или <c>Local</c></param>
/// <param name="Repository">Для репозитория: <c>owner/repo</c> на GitHub</param>
/// <param name="Branch">Для репозитория: ветка или null — по умолчанию</param>
/// <param name="ReleaseRepository">Для .dat из релиза: <c>owner/repo</c></param>
/// <param name="Asset">Для .dat из релиза: имя файла</param>
/// <param name="ChecksumAsset">Для .dat из релиза: файл контрольной суммы или null</param>
/// <param name="Url">Для .dat по ссылке: http(s)-адрес</param>
/// <param name="ChecksumUrl">Для .dat по ссылке: адрес контрольной суммы SHA-256 или null</param>
/// <param name="AutoUpdate">Обновлять по расписанию</param>
public sealed record DomainSourceSaveRequest(
    string Name,
    string Kind,
    string? Repository = null,
    string? Branch = null,
    string? ReleaseRepository = null,
    string? Asset = null,
    string? ChecksumAsset = null,
    string? Url = null,
    string? ChecksumUrl = null,
    bool AutoUpdate = true);
