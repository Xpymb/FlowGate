namespace FlowGate.SharedContracts.Dto.Xray;

/// <summary>
///     Релиз Xray, доступный для установки
/// </summary>
/// <param name="Version">Версия, например <c>26.10.31</c></param>
/// <param name="Tag">Тег релиза, например <c>v26.10.31</c></param>
/// <param name="PublishedAt">Дата публикации</param>
/// <param name="IsPrerelease">Предварительный релиз</param>
/// <param name="SizeBytes">Размер архива, байты</param>
/// <param name="IsInstalled">Эта версия установлена сейчас</param>
/// <param name="IsLatest">Последняя стабильная версия</param>
public sealed record XrayReleaseDto(
    string Version,
    string Tag,
    DateTimeOffset? PublishedAt,
    bool IsPrerelease,
    long SizeBytes,
    bool IsInstalled,
    bool IsLatest);
