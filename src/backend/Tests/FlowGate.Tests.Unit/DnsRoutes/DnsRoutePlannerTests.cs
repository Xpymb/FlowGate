using FlowGate.Contracts.DnsRoutes;
using FlowGate.Contracts.DomainLists;

namespace FlowGate.Tests.Unit.DnsRoutes;

/// <summary>
///     Тесты <see cref="DnsRoutePlanner"/>: каждая строка таблицы правил этапа 5 отдельным тестом
/// </summary>
public sealed class DnsRoutePlannerTests
{
    private const string Base = "fg-youtube";
    private const string Description = "FlowGate: youtube (v2fly)";
    private static readonly HashSet<string> NoRecreated = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    ///     Новый маршрут: группа, описание, элементы и включённое правило
    /// </summary>
    [Fact]
    public void Plan_NewRoute_CreatesGroupAndEnabledRule()
    {
        var plan = Plan([Target(Route(), "a.com", "1.2.3.0/24")], NdmsDnsState.Empty);

        Assert.Equal(
            [
                "object-group fqdn fg-youtube",
                "object-group fqdn fg-youtube include a.com",
                "object-group fqdn fg-youtube include 1.2.3.0/24",
                $"object-group fqdn fg-youtube description \"{Description}\"",
                "dns-proxy route object-group fg-youtube Proxy0 auto",
            ],
            plan.Commands);
        var outcome = Assert.Single(plan.Outcomes);
        Assert.Equal((DnsRouteOutcomeKind.Applied, false, 1, 2), (outcome.Kind, outcome.Disabled, outcome.Parts, outcome.Entries));
    }

    /// <summary>
    ///     Повторный прогон без изменений — ни одной команды
    /// </summary>
    [Fact]
    public void Plan_NothingChanged_NoCommands()
    {
        var state = State([Group(Base, Description, "a.com", "b.com")], [Rule(Base, "Proxy0")]);

        var plan = Plan([Target(Provisioned(), "a.com", "b.com")], state);

        Assert.Empty(plan.Commands);
    }

    /// <summary>
    ///     Добавление одного домена — одна команда; удаление — одна <c>no include</c>
    /// </summary>
    [Fact]
    public void Plan_ContentDiff_OnlyDifference()
    {
        var state = State([Group(Base, Description, "a.com", "old.com")], [Rule(Base, "Proxy0")]);

        var plan = Plan([Target(Provisioned(), "a.com", "new.com")], state);

        Assert.Equal(
            ["no object-group fqdn fg-youtube include old.com", "object-group fqdn fg-youtube include new.com"],
            plan.Commands);
    }

    /// <summary>
    ///     Выключенное пользователем правило остаётся выключенным при обновлении состава
    /// </summary>
    [Fact]
    public void Plan_RuleDisabledByUser_StaysDisabled()
    {
        var state = State([Group(Base, Description, "a.com")], [Rule(Base, "Proxy0", disabled: true)]);

        var plan = Plan([Target(Provisioned(), "a.com", "b.com")], state);

        Assert.Equal(["object-group fqdn fg-youtube include b.com"], plan.Commands);
        Assert.True(plan.Outcomes[0].Disabled);
    }

    /// <summary>
    ///     Правило удалено пользователем, группа на месте — правило возвращается выключенным
    /// </summary>
    [Fact]
    public void Plan_RuleDeletedByUser_RestoredDisabled()
    {
        var state = State([Group(Base, Description, "a.com")], []);

        var plan = Plan([Target(Provisioned(), "a.com")], state);

        Assert.Equal(["dns-proxy route object-group fg-youtube Proxy0 auto", "dns-proxy route disable"], plan.Commands);
        Assert.True(plan.Outcomes[0].Disabled);
    }

    /// <summary>
    ///     Основная группа удалена пользователем — маршрут удаляется, оставшиеся части и висящие правила снимаются
    /// </summary>
    [Fact]
    public void Plan_GroupDeletedByUser_RemovesRouteAndLeftovers()
    {
        var state = State([Group("fg-youtube-2", Description + " 2", "c.com")], [Rule(Base, "Proxy0"), Rule("fg-youtube-2", "Proxy0")]);

        var plan = Plan([Target(Provisioned(), "a.com")], state);

        Assert.Equal(DnsRouteOutcomeKind.RemovedByUser, plan.Outcomes[0].Kind);
        Assert.Equal(
            [
                "no dns-proxy route object-group fg-youtube Proxy0",
                "no dns-proxy route object-group fg-youtube-2 Proxy0",
                "no object-group fqdn fg-youtube-2",
            ],
            plan.Commands);
    }

    /// <summary>
    ///     Интерфейс пересоздан сверкой (Keenetic убрал правила с ним) — правило возвращается с прежним флагом
    /// </summary>
    [Fact]
    public void Plan_InterfaceRecreated_RestoresPreviousFlag()
    {
        var state = State([Group(Base, Description, "a.com")], []);
        var recreated = new HashSet<string>(["Proxy0"], StringComparer.OrdinalIgnoreCase);

        var enabled = DnsRoutePlanner.Plan([Target(Provisioned(), "a.com")], state, recreated, 1000);
        var disabled = DnsRoutePlanner.Plan([Target(Provisioned() with { LastKnownDisabled = true }, "a.com")], state, recreated, 1000);

        Assert.Equal(["dns-proxy route object-group fg-youtube Proxy0 auto"], enabled.Commands);
        Assert.Equal(["dns-proxy route object-group fg-youtube Proxy0 auto", "dns-proxy route disable"], disabled.Commands);
    }

    /// <summary>
    ///     Смена соединения: правило переносится на новый интерфейс с прежним флагом
    /// </summary>
    [Fact]
    public void Plan_ConnectionChanged_MovesRuleWithFlag()
    {
        var state = State([Group(Base, Description, "a.com")], [Rule(Base, "Proxy0", disabled: true)]);

        var plan = Plan([Target(Provisioned(), "a.com") with { InterfaceName = "Wireguard0" }], state);

        Assert.Equal(
            [
                "no dns-proxy route object-group fg-youtube Proxy0",
                "dns-proxy route object-group fg-youtube Wireguard0 auto",
                "dns-proxy route disable",
            ],
            plan.Commands);
    }

    /// <summary>
    ///     Явное включение из FlowGate пересоздаёт правило без disable
    /// </summary>
    [Fact]
    public void Plan_RequestedEnable_RecreatesRule()
    {
        var state = State([Group(Base, Description, "a.com")], [Rule(Base, "Proxy0", disabled: true)]);

        var plan = Plan([Target(Provisioned() with { RequestedDisabled = false }, "a.com")], state);

        Assert.Equal(["no dns-proxy route object-group fg-youtube Proxy0", "dns-proxy route object-group fg-youtube Proxy0 auto"], plan.Commands);
        Assert.False(plan.Outcomes[0].Disabled);
    }

    /// <summary>
    ///     Больше лимита — части <c>-2</c>, <c>-3</c>, у каждой правило с флагом основной
    /// </summary>
    [Fact]
    public void Plan_OverLimit_SplitsIntoPartsInheritingFlag()
    {
        var state = State([Group(Base, Description, "a.com", "b.com")], [Rule(Base, "Proxy0", disabled: true)]);

        var plan = DnsRoutePlanner.Plan([Target(Provisioned(), "a.com", "b.com", "c.com", "d.com", "e.com")], state, NoRecreated, 2);

        Assert.Equal(3, plan.Outcomes[0].Parts);
        Assert.Contains("object-group fqdn fg-youtube-3 include e.com", plan.Commands);
        Assert.Contains($"object-group fqdn fg-youtube-2 description \"{Description} 2\"", plan.Commands);
        Assert.Equal(2, plan.Commands.Count(command => command == "dns-proxy route disable"));
        Assert.DoesNotContain(plan.Commands, command => command.Contains("fg-youtube include", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Список сократился — опустевшая часть удаляется вместе со своим правилом
    /// </summary>
    [Fact]
    public void Plan_Shrunk_RemovesEmptyPartWithRule()
    {
        var state = State(
            [Group(Base, Description, "a.com", "b.com"), Group("fg-youtube-2", Description + " 2", "c.com")],
            [Rule(Base, "Proxy0"), Rule("fg-youtube-2", "Proxy0")]);

        var plan = DnsRoutePlanner.Plan([Target(Provisioned(), "a.com", "b.com")], state, NoRecreated, 2);

        Assert.Equal(
            [
                "no object-group fqdn fg-youtube-2 include c.com",
                "no dns-proxy route object-group fg-youtube-2 Proxy0",
                "no object-group fqdn fg-youtube-2",
            ],
            plan.Commands);
    }

    /// <summary>
    ///     Источник ещё не загружен и групп нет — ждём, команд нет
    /// </summary>
    [Fact]
    public void Plan_ContentUnavailable_Waits()
    {
        var plan = Plan([new DnsRouteTarget(Route(), "Proxy0", Description, null)], NdmsDnsState.Empty);

        Assert.Empty(plan.Commands);
        Assert.Equal(DnsRouteOutcomeKind.Waiting, plan.Outcomes[0].Kind);
    }

    /// <summary>
    ///     Группа FlowGate без маршрута (маршрут удалён в FlowGate) убирается вместе с правилами; чужие группы не трогаются
    /// </summary>
    [Fact]
    public void Plan_Orphans_RemovesOwnedOnly()
    {
        var state = State(
            [Group("fg-old", "FlowGate: old (v2fly)", "x.com"), Group("fg-user", "Мой список", "y.com"), Group("work", "Работа", "z.com")],
            [Rule("fg-old", "Proxy1"), Rule("fg-gone", "Proxy1"), Rule("work", "Proxy1")]);

        var plan = Plan([], state);

        Assert.Equal(
            ["no dns-proxy route object-group fg-old Proxy1", "no dns-proxy route object-group fg-gone Proxy1", "no object-group fqdn fg-old"],
            plan.Commands);
    }

    /// <summary>
    ///     Правило нашей группы на чужой интерфейс не трогается
    /// </summary>
    [Fact]
    public void Plan_ForeignInterfaceRule_LeftAlone()
    {
        var state = State([Group(Base, Description, "a.com")], [Rule(Base, "Proxy0"), Rule(Base, "Wireguard5")]);

        var plan = Plan([Target(Provisioned(), "a.com")], state);

        Assert.Empty(plan.Commands);
    }

    /// <summary>
    ///     Части группы определяются по имени точно: <c>fg-youtube-r2</c> — не часть <c>fg-youtube</c>
    /// </summary>
    [Fact]
    public void PartIndex_DistinguishesSuffixes()
    {
        Assert.Equal(1, DnsRoute.PartIndex(Base, Base));
        Assert.Equal(3, DnsRoute.PartIndex(Base, "fg-youtube-3"));
        Assert.Null(DnsRoute.PartIndex(Base, "fg-youtube-r2"));
        Assert.Null(DnsRoute.PartIndex(Base, "fg-youtube-1"));
        Assert.Null(DnsRoute.PartIndex(Base, "fg-youtube-02"));
    }

    private static DnsRoutePlan Plan(IReadOnlyList<DnsRouteTarget> targets, NdmsDnsState state) =>
        DnsRoutePlanner.Plan(targets, state, NoRecreated, 1000);

    private static DnsRoute Route() =>
        new("rt-1", "v2fly", "youtube", null, "con-1", Base, false, null, false, null, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private static DnsRoute Provisioned() => Route() with { Provisioned = true, AppliedInterface = "Proxy0" };

    private static DnsRouteTarget Target(DnsRoute route, params string[] entries) =>
        new(route, "Proxy0", Description, new ListContent(entries, 0, 0, 0));

    private static NdmsGroup Group(string name, string description, params string[] entries) => new(name, description, entries);

    private static NdmsRoute Rule(string group, string interfaceName, bool disabled = false) => new(group, interfaceName, true, disabled);

    private static NdmsDnsState State(IReadOnlyList<NdmsGroup> groups, IReadOnlyList<NdmsRoute> routes) =>
        new(groups.ToDictionary(group => group.Name, StringComparer.Ordinal), routes);
}
