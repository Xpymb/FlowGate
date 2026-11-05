namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Сборка списка v2fly с раскрытием <c>include</c> внутри одного источника. Семантика повторяет keenetic-geosite-sync
///     (<c>resolveRules</c>): фильтр применяется ко всем правилам списка, включая <c>include</c>; у <c>include:x @a @b</c>
///     вложенный список берётся отдельно с каждым атрибутом как фильтром; повтор одного и того же вложения пропускается
/// </summary>
public sealed class V2flyListResolver
{
    private readonly IReadOnlyDictionary<string, IReadOnlyList<V2flyRule>> _lists;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="V2flyListResolver"/>
    /// </summary>
    /// <param name="lists">Правила всех списков источника по имени</param>
    /// <exception cref="ArgumentNullException">Если lists равен null</exception>
    public V2flyListResolver(IReadOnlyDictionary<string, IReadOnlyList<V2flyRule>> lists)
    {
        _lists = lists ?? throw new ArgumentNullException(nameof(lists));
    }

    /// <summary>
    ///     Собирает список
    /// </summary>
    /// <param name="name">Имя списка</param>
    /// <param name="attributeFilter">Фильтр <c>attr</c> / <c>!attr</c> или null</param>
    /// <returns>Содержимое для Keenetic</returns>
    /// <exception cref="DomainListException">Если списка или вложенного списка нет либо include образуют цикл</exception>
    public ListContent Resolve(string name, string? attributeFilter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var builder = new ListContentBuilder();
        Collect(name, attributeFilter, builder, new HashSet<string>(StringComparer.Ordinal), []);
        return builder.Build();
    }

    /// <summary>
    ///     Собирает все атрибуты, которые встречаются в списке и его вложениях
    /// </summary>
    /// <param name="name">Имя списка</param>
    /// <returns>Атрибуты по алфавиту</returns>
    /// <exception cref="DomainListException">Если списка или вложенного списка нет либо include образуют цикл</exception>
    public IReadOnlyList<string> CollectAttributes(string name)
    {
        var attributes = new SortedSet<string>(StringComparer.Ordinal);
        Walk(name, [], new HashSet<string>(StringComparer.Ordinal), rule =>
        {
            if (rule.Kind != V2flyRuleKind.Include)
            {
                attributes.UnionWith(rule.Attributes);
            }
        });
        return attributes.ToList();
    }

    private void Collect(string name, string? filter, ListContentBuilder builder, HashSet<string> included, List<string> stack)
    {
        foreach (var rule in Enter(name, stack).Where(rule => rule.Matches(filter)))
        {
            switch (rule.Kind)
            {
                case V2flyRuleKind.Include:
                {
                    var childFilters = rule.Attributes.Count == 0 ? [null] : rule.Attributes.Cast<string?>().ToList();
                    foreach (var childFilter in childFilters)
                    {
                        var key = childFilter is null ? rule.Value : $"{rule.Value}@{childFilter}";
                        if (included.Add(key))
                        {
                            Collect(rule.Value, childFilter, builder, included, [.. stack, name]);
                        }
                    }

                    break;
                }
                case V2flyRuleKind.Keyword:
                {
                    builder.SkipKeyword();
                    break;
                }
                case V2flyRuleKind.Regexp:
                {
                    builder.SkipRegexp();
                    break;
                }
                default:
                {
                    builder.AddNormalized(rule.Value);
                    break;
                }
            }
        }
    }

    private void Walk(string name, List<string> stack, HashSet<string> visited, Action<V2flyRule> visit)
    {
        var rules = Enter(name, stack);
        if (!visited.Add(name))
        {
            return;
        }

        foreach (var rule in rules)
        {
            visit(rule);
            if (rule.Kind == V2flyRuleKind.Include)
            {
                Walk(rule.Value, [.. stack, name], visited, visit);
            }
        }
    }

    private IReadOnlyList<V2flyRule> Enter(string name, List<string> stack)
    {
        if (stack.Contains(name, StringComparer.Ordinal))
        {
            throw new DomainListException($"include образуют цикл: {string.Join(" → ", [.. stack, name])}");
        }

        if (_lists.TryGetValue(name, out var rules))
        {
            return rules;
        }

        throw new DomainListException(stack.Count == 0
            ? $"Списка «{name}» нет в источнике"
            : $"Список «{stack[^1]}» включает несуществующий «{name}»");
    }
}
