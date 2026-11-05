namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Накопитель содержимого списка: нормализует значения, убирает дубликаты и считает пропуски
/// </summary>
public sealed class ListContentBuilder
{
    private readonly List<string> _entries = [];
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
    private int _skippedKeyword;
    private int _skippedRegexp;
    private int _skippedInvalid;

    /// <summary>
    ///     Добавляет домен, адрес или подсеть; некорректное значение учитывается как пропуск
    /// </summary>
    /// <param name="value">Значение</param>
    public void Add(string value)
    {
        if (!ListEntries.TryNormalize(value, out var entry, out _))
        {
            SkipInvalid();
            return;
        }

        AddNormalized(entry);
    }

    /// <summary>
    ///     Добавляет уже нормализованный элемент
    /// </summary>
    /// <param name="entry">Элемент</param>
    public void AddNormalized(string entry)
    {
        if (_seen.Add(entry))
        {
            _entries.Add(entry);
        }
    }

    /// <summary>
    ///     Учитывает отброшенное некорректное значение
    /// </summary>
    public void SkipInvalid() => _skippedInvalid++;

    /// <summary>
    ///     Учитывает пропущенное правило <c>keyword:</c>
    /// </summary>
    public void SkipKeyword() => _skippedKeyword++;

    /// <summary>
    ///     Учитывает пропущенное правило <c>regexp:</c>
    /// </summary>
    public void SkipRegexp() => _skippedRegexp++;

    /// <summary>
    ///     Возвращает накопленное содержимое
    /// </summary>
    /// <returns>Содержимое списка</returns>
    public ListContent Build() => new(_entries.ToList(), _skippedKeyword, _skippedRegexp, _skippedInvalid);
}
