using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Exceptions;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Keenetic;

/// <summary>
///     Тесты <see cref="KeeneticDnsParser"/> на ответах RCI, снятых с роутера (шаг 0 этапа 5)
/// </summary>
public sealed class KeeneticDnsParserTests
{
    /// <summary>
    ///     Группа: описание и элементы как их хранит Keenetic
    /// </summary>
    [Fact]
    public void ParseGroups_Fixture_ReadsDescriptionAndEntries()
    {
        var groups = KeeneticDnsParser.ParseGroups(RciFixtures.ToElement(RciFixtures.ObjectGroupFqdn));

        var group = groups["fgspike"];
        Assert.Equal("FlowGate: spike (test)", group.Description);
        Assert.Equal(7, group.Entries.Count);
        Assert.Contains("91.108.56.1/22", group.Entries);
    }

    /// <summary>
    ///     Правила: флаги auto и disable
    /// </summary>
    [Fact]
    public void ParseRoutes_Fixture_ReadsFlags()
    {
        var routes = KeeneticDnsParser.ParseRoutes(RciFixtures.ToElement(RciFixtures.DnsProxyRoute));

        Assert.Equal(2, routes.Count);
        Assert.True(routes[0].Disabled);
        Assert.False(routes[1].Disabled);
        Assert.True(routes[1].Auto);
        Assert.Equal("Proxy9", routes[0].Interface);
    }

    /// <summary>
    ///     Пустые ответы Keenetic: <c>{}</c> и <c>[]</c>
    /// </summary>
    [Fact]
    public void Parse_Empty_ReturnsEmpty()
    {
        Assert.Empty(KeeneticDnsParser.ParseGroups(RciFixtures.ToElement("{}")));
        Assert.Empty(KeeneticDnsParser.ParseRoutes(RciFixtures.ToElement("[]")));
    }

    /// <summary>
    ///     Ответ неожиданного вида — RCI неисправен
    /// </summary>
    [Fact]
    public void Parse_WrongShape_Throws()
    {
        Assert.Throws<KeeneticUnavailableException>(() => KeeneticDnsParser.ParseGroups(RciFixtures.ToElement("[]")));
        Assert.Throws<KeeneticUnavailableException>(() => KeeneticDnsParser.ParseRoutes(RciFixtures.ToElement("{}")));
    }
}
