using FlowGate.Contracts.DomainLists;

namespace FlowGate.Tests.Unit.DomainLists;

/// <summary>
///     Тесты <see cref="V2flyListResolver"/> на фикстурах keenetic-geosite-sync (<c>tests/fixtures/data/attr-*</c>)
/// </summary>
public sealed class V2flyListResolverTests
{
    private readonly V2flyListResolver _sut = new(Lists(
        ("attr-base", "foo.com\nbar.com @Ads\nbaz.com @video\nqux.com @ads @video\nregexp:^DoNotLower$ @ads\nkeyword:IgnoredKeyword @ads\n"),
        ("attr-include", "include:attr-base @ads\ninclude:attr-base @!ads\n"),
        ("attr-include-dup", "include:attr-base\ninclude:attr-base @ads\ninclude:attr-base @ads\n"),
        ("attr-include-neg", "include:attr-base @!ads\n"),
        ("cycle-a", "a.com\ninclude:cycle-b\n"),
        ("cycle-b", "include:cycle-a\n"),
        ("broken", "include:missing\n"),
        ("telegram", "telegram.org\n91.108.56.1/22\ntelegram.org\n")));

    /// <summary>
    ///     include с атрибутом и с отрицанием собирает объединение, keyword и regexp считаются пропущенными
    /// </summary>
    [Fact]
    public void Resolve_IncludeWithAttributes_MatchesReference()
    {
        var content = _sut.Resolve("attr-include", null);

        Assert.Equal(["bar.com", "baz.com", "foo.com", "qux.com"], content.Entries.Order());
        Assert.Equal((1, 1), (content.SkippedKeyword, content.SkippedRegexp));
    }

    /// <summary>
    ///     Отрицание <c>@!ads</c> берёт только правила без атрибута
    /// </summary>
    [Fact]
    public void Resolve_NegatedInclude_ExcludesAttribute()
    {
        var content = _sut.Resolve("attr-include-neg", null);

        Assert.Equal(["baz.com", "foo.com"], content.Entries.Order());
        Assert.Equal(0, content.Skipped);
    }

    /// <summary>
    ///     Одинаковое вложение повторно не раскрывается; пропуски считаются как в Go-сборщике v2fly
    /// </summary>
    [Fact]
    public void Resolve_DuplicateInclude_CountsLikeReference()
    {
        var content = _sut.Resolve("attr-include-dup", null);

        Assert.Equal(["bar.com", "baz.com", "foo.com", "qux.com"], content.Entries.Order());
        Assert.Equal((2, 2), (content.SkippedKeyword, content.SkippedRegexp));
    }

    /// <summary>
    ///     Фильтр маршрута применяется к самому списку
    /// </summary>
    [Fact]
    public void Resolve_FilterOnList_KeepsMatching()
    {
        Assert.Equal(["bar.com", "qux.com"], _sut.Resolve("attr-base", "ads").Entries);
        Assert.Equal(["foo.com", "baz.com"], _sut.Resolve("attr-base", "!ads").Entries);
    }

    /// <summary>
    ///     Дубликаты убираются, подсеть нормализована, порядок сохраняется
    /// </summary>
    [Fact]
    public void Resolve_MixedList_DeduplicatesAndNormalizes()
    {
        Assert.Equal(["telegram.org", "91.108.56.0/22"], _sut.Resolve("telegram", null).Entries);
    }

    /// <summary>
    ///     Цикл include и отсутствующий список — понятные ошибки
    /// </summary>
    [Fact]
    public void Resolve_CycleOrMissing_Throws()
    {
        Assert.Contains("цикл", Assert.Throws<DomainListException>(() => _sut.Resolve("cycle-a", null)).Message);
        Assert.Contains("missing", Assert.Throws<DomainListException>(() => _sut.Resolve("broken", null)).Message);
        Assert.Throws<DomainListException>(() => _sut.Resolve("nope", null));
    }

    /// <summary>
    ///     Атрибуты собираются вместе с вложенными списками
    /// </summary>
    [Fact]
    public void CollectAttributes_WithIncludes_ReturnsSorted()
    {
        Assert.Equal(["ads", "video"], _sut.CollectAttributes("attr-include"));
    }

    private static Dictionary<string, IReadOnlyList<V2flyRule>> Lists(params (string Name, string Text)[] lists) =>
        lists.ToDictionary(list => list.Name, list => V2flyListParser.Parse(list.Text).Rules, StringComparer.Ordinal);
}
