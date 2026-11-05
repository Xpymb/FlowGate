using FlowGate.Contracts.DomainLists;

namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Кеш каталога списков источника (<c>sources/&lt;id&gt;.catalog.json</c>): строится при обновлении, чтобы не читать
///     файл источника на каждый запрос UI
/// </summary>
/// <param name="Sha256">SHA-256 файла, по которому построен каталог (для локального — хеш текстов)</param>
/// <param name="Lists">Списки по алфавиту</param>
/// <param name="Errors">Ошибки разбора отдельных списков</param>
public sealed record SourceListCatalog(string Sha256, IReadOnlyList<CatalogEntry> Lists, IReadOnlyList<string> Errors);
