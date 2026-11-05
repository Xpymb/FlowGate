using System.Security.Cryptography;
using System.Text;

using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources.Readers;

/// <summary>
///     Набор текстовых списков v2fly (репозиторий или локальный источник): разбор, каталог и сборка списка
/// </summary>
public sealed class V2flyTextLists
{
    private readonly Dictionary<string, IReadOnlyList<V2flyRule>> _lists;
    private readonly List<string> _errors;

    private V2flyTextLists(Dictionary<string, IReadOnlyList<V2flyRule>> lists, List<string> errors)
    {
        _lists = lists;
        _errors = errors;
    }

    /// <summary>
    ///     Ошибки разбора: недопустимые имена и строки с ошибками
    /// </summary>
    public IReadOnlyList<string> Errors => _errors;

    /// <summary>
    ///     Разбирает тексты списков
    /// </summary>
    /// <param name="files">Пары «имя списка — текст»; имя проверяется по правилам v2fly</param>
    /// <param name="pathPrefix">Префикс пути для сообщений об ошибках, например <c>data/</c></param>
    /// <returns>Разобранный набор</returns>
    public static V2flyTextLists Parse(IEnumerable<(string Name, string Text)> files, string pathPrefix)
    {
        ArgumentNullException.ThrowIfNull(files);

        var lists = new Dictionary<string, IReadOnlyList<V2flyRule>>(StringComparer.Ordinal);
        var errors = new List<string>();
        foreach (var (name, text) in files)
        {
            if (!V2flyListParser.IsValidListName(name))
            {
                errors.Add($"{pathPrefix}{name}: недопустимое имя списка (нужно [a-z0-9!._-])");
                continue;
            }

            var parsed = V2flyListParser.Parse(text);
            lists[name] = parsed.Rules;
            errors.AddRange(parsed.Errors.Select(error => $"{pathPrefix}{name}: {error}"));
        }

        return new V2flyTextLists(lists, errors);
    }

    /// <summary>
    ///     Хеш набора текстов для кеша каталога
    /// </summary>
    /// <param name="files">Пары «имя — текст»</param>
    /// <returns>SHA-256 в hex</returns>
    public static string Hash(IEnumerable<(string Name, string Text)> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var (name, text) in files.OrderBy(file => file.Name, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes($"{name}\n{text}\n\0"));
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    /// <summary>
    ///     Строит каталог: каждый список собирается без фильтра; не собравшиеся попадают в ошибки
    /// </summary>
    /// <param name="sha256">Хеш исходных данных</param>
    /// <returns>Каталог</returns>
    public SourceListCatalog BuildCatalog(string sha256)
    {
        var resolver = new V2flyListResolver(_lists);
        var entries = new List<CatalogEntry>();
        var errors = new List<string>(_errors);

        foreach (var name in _lists.Keys.Order(StringComparer.Ordinal))
        {
            try
            {
                entries.Add(CatalogEntry.From(name, resolver.Resolve(name, null), resolver.CollectAttributes(name)));
            }
            catch (DomainListException ex)
            {
                errors.Add($"{name}: {ex.Message}");
            }
        }

        return new SourceListCatalog(sha256, entries, errors);
    }

    /// <summary>
    ///     Собирает список
    /// </summary>
    /// <param name="name">Имя</param>
    /// <param name="attributeFilter">Фильтр или null</param>
    /// <returns>Содержимое</returns>
    /// <exception cref="DomainListException">Если списка нет или он не собирается</exception>
    public ListContent Resolve(string name, string? attributeFilter) => new V2flyListResolver(_lists).Resolve(name, attributeFilter);
}
