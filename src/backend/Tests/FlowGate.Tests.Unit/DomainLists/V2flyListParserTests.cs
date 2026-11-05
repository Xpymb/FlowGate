using FlowGate.Contracts.DomainLists;

namespace FlowGate.Tests.Unit.DomainLists;

/// <summary>
///     Тесты <see cref="V2flyListParser"/>: формат v2fly domain-list-community и строки-адреса FlowGate
/// </summary>
public sealed class V2flyListParserTests
{
    /// <summary>
    ///     Все виды строк разбираются; атрибуты в нижнем регистре; regexp сохраняет регистр
    /// </summary>
    [Fact]
    public void Parse_AllRuleKinds_ReturnsRules()
    {
        var result = V2flyListParser.Parse(
            """
            # комментарий
            Example.com @Ads
            domain:sub.example.com
            full:www.example.org # хвост
            keyword:Tracker
            regexp:^DoNotLower$ @ads=1
            include:other @video
            include extra
            91.108.4.0/22
            2001:67c:4e8::/48
            """);

        Assert.Empty(result.Errors);
        Assert.Equal(
            [
                new V2flyRule(V2flyRuleKind.Domain, "example.com", ["ads"]),
                new V2flyRule(V2flyRuleKind.Domain, "sub.example.com", []),
                new V2flyRule(V2flyRuleKind.Full, "www.example.org", []),
                new V2flyRule(V2flyRuleKind.Keyword, "tracker", []),
                new V2flyRule(V2flyRuleKind.Regexp, "^DoNotLower$", ["ads"]),
                new V2flyRule(V2flyRuleKind.Include, "other", ["video"]),
                new V2flyRule(V2flyRuleKind.Include, "extra", []),
                new V2flyRule(V2flyRuleKind.Address, "91.108.4.0/22", []),
                new V2flyRule(V2flyRuleKind.Address, "2001:67c:4e8::/48", []),
            ],
            result.Rules,
            new RuleComparer());
    }

    /// <summary>
    ///     Ошибочные строки пропускаются и попадают в ошибки с номером строки (как <c>include:</c> и <c>foo @@@</c> в keenetic-geosite-sync)
    /// </summary>
    [Fact]
    public void Parse_BrokenLines_ReportsLineNumbers()
    {
        var result = V2flyListParser.Parse("good.com\ninclude:\nfoo.com @@@\nпример.рф\nfull:\n");

        Assert.Single(result.Rules);
        Assert.Equal([2, 3, 4, 5], result.Errors.Select(error => error.Line));
        Assert.StartsWith("строка 2:", result.Errors[0].ToString());
    }

    /// <summary>
    ///     Имена списков и фильтры проверяются по правилам v2fly
    /// </summary>
    [Fact]
    public void IsValid_NamesAndFilters()
    {
        Assert.True(V2flyListParser.IsValidListName("geolocation-!cn"));
        Assert.False(V2flyListParser.IsValidListName("Telegram"));
        Assert.True(V2flyListParser.IsValidAttributeFilter("!ads"));
        Assert.False(V2flyListParser.IsValidAttributeFilter("@ads"));
    }

    private sealed class RuleComparer : IEqualityComparer<V2flyRule>
    {
        public bool Equals(V2flyRule? x, V2flyRule? y) =>
            x is not null && y is not null && x.Kind == y.Kind && x.Value == y.Value && x.Attributes.SequenceEqual(y.Attributes);

        public int GetHashCode(V2flyRule obj) => HashCode.Combine(obj.Kind, obj.Value);
    }
}
