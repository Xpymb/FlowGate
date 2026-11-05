namespace FlowGate.Contracts.DnsRoutes;

/// <summary>
///     Планировщик DNS-маршрутов: сравнивает желаемое с тем, что есть в Keenetic, и выдаёт минимальный набор команд.
///     Правила (docs/keenetic-findings.md §100, план этапа 5):
///     <list type="bullet">
///         <item>правило включается только при первом создании; выключенное пользователем остаётся выключенным;</item>
///         <item>маршрут, снятый командой <c>flowgate cleanup</c> (переустановка), создаётся заново с прежним флагом;</item>
///         <item>правило удалено пользователем, группа на месте — восстанавливается выключенным;</item>
///         <item>основная группа удалена пользователем — маршрут удаляется и из FlowGate;</item>
///         <item>интерфейс пересоздан сверкой (Keenetic убрал правила вместе с ним) — правило возвращается с прежним флагом;</item>
///         <item>состав меняется только по разнице, элементы остаются в своих частях;</item>
///         <item>удаление группы не удаляет её правила, поэтому правила снимаются явно.</item>
///     </list>
/// </summary>
public static class DnsRoutePlanner
{
    /// <summary>
    ///     Строит план
    /// </summary>
    /// <param name="targets">Маршруты FlowGate с интерфейсами и содержимым</param>
    /// <param name="state">Состояние Keenetic</param>
    /// <param name="recreatedInterfaces">Интерфейсы, которые сверка только что создала заново</param>
    /// <param name="maxEntriesPerGroup">Лимит элементов в группе</param>
    /// <returns>Команды и итоги</returns>
    public static DnsRoutePlan Plan(
        IReadOnlyList<DnsRouteTarget> targets,
        NdmsDnsState state,
        IReadOnlySet<string> recreatedInterfaces,
        int maxEntriesPerGroup)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(recreatedInterfaces);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxEntriesPerGroup, 1);

        var commands = new List<string>();
        var outcomes = new List<DnsRouteOutcome>();
        var claimed = new HashSet<string>(StringComparer.Ordinal);

        foreach (var target in targets)
        {
            outcomes.Add(PlanRoute(target, state, recreatedInterfaces, maxEntriesPerGroup, commands, claimed));
        }

        RemoveOrphans(state, claimed, commands);
        return new DnsRoutePlan(commands, outcomes);
    }

    /// <summary>
    ///     Команды полного удаления маршрута из Keenetic: правила всех частей на любые интерфейсы, затем сами группы
    /// </summary>
    /// <param name="route">Маршрут</param>
    /// <param name="state">Состояние Keenetic</param>
    /// <returns>Команды</returns>
    public static IReadOnlyList<string> Remove(DnsRoute route, NdmsDnsState state)
    {
        ArgumentNullException.ThrowIfNull(route);
        ArgumentNullException.ThrowIfNull(state);

        var commands = new List<string>();
        RemoveRouteGroups(route.GroupBaseName, state, commands);
        return commands;
    }

    /// <summary>
    ///     Команды удаления всего, что FlowGate создал в Keenetic: правила групп <c>fg-*</c> с описанием FlowGate и висящие
    ///     правила <c>fg-*</c> без групп, затем сами группы. Чужие группы и правила не затрагиваются
    /// </summary>
    /// <param name="state">Состояние Keenetic</param>
    /// <returns>Команды; пусто — удалять нечего</returns>
    public static IReadOnlyList<string> RemoveAll(NdmsDnsState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var commands = new List<string>();
        RemoveOrphans(state, new HashSet<string>(StringComparer.Ordinal), commands);
        return commands;
    }

    private static DnsRouteOutcome PlanRoute(
        DnsRouteTarget target,
        NdmsDnsState state,
        IReadOnlySet<string> recreatedInterfaces,
        int maxEntries,
        List<string> commands,
        HashSet<string> claimed)
    {
        var route = target.Route;
        var baseName = route.GroupBaseName;
        var observedParts = ObservedParts(baseName, state);
        claimed.UnionWith(observedParts.Select(part => part.Name));
        claimed.Add(baseName);

        var baseExists = state.Groups.ContainsKey(baseName);
        if (route.Provisioned && !baseExists)
        {
            RemoveRouteGroups(baseName, state, commands);
            return new DnsRouteOutcome(route.Id, DnsRouteOutcomeKind.RemovedByUser, route.LastKnownDisabled, 0, 0);
        }

        if (target.Content is null && !baseExists)
        {
            return new DnsRouteOutcome(route.Id, DnsRouteOutcomeKind.Waiting, route.LastKnownDisabled, 0, 0);
        }

        var parts = target.Content is null
            ? observedParts.Select(part => new PlannedPart(part.Name, part.Index, part.Entries.ToList(), exists: true)).ToList()
            : PlaceEntries(baseName, observedParts, target.Content.Entries, maxEntries, state, commands);

        foreach (var part in parts)
        {
            claimed.Add(part.Name);
            var description = part.Index == 1 ? target.Description : $"{target.Description} {part.Index}";
            var observedDescription = state.Groups.TryGetValue(part.Name, out var group) ? group.Description : null;
            if (observedDescription != description)
            {
                commands.Add($"object-group fqdn {part.Name} description \"{description}\"");
            }
        }

        var disabled = PlanRules(target, parts, state, recreatedInterfaces, commands);
        return new DnsRouteOutcome(route.Id, DnsRouteOutcomeKind.Applied, disabled, parts.Count, parts.Sum(part => part.Entries.Count));
    }

    private static List<PlannedPart> PlaceEntries(
        string baseName,
        IReadOnlyList<(string Name, int Index, IReadOnlyList<string> Entries)> observedParts,
        IReadOnlyList<string> desired,
        int maxEntries,
        NdmsDnsState state,
        List<string> commands)
    {
        var desiredSet = desired.ToHashSet(StringComparer.Ordinal);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var parts = new List<PlannedPart>();

        foreach (var observed in observedParts)
        {
            var keep = new List<string>();
            foreach (var entry in observed.Entries)
            {
                if (desiredSet.Contains(entry) && !placed.Contains(entry) && keep.Count < maxEntries)
                {
                    keep.Add(entry);
                    placed.Add(entry);
                }
                else
                {
                    commands.Add($"no object-group fqdn {observed.Name} include {entry}");
                }
            }

            parts.Add(new PlannedPart(observed.Name, observed.Index, keep, exists: true));
        }

        if (parts.All(part => part.Index != 1))
        {
            parts.Insert(0, new PlannedPart(baseName, 1, [], exists: false));
        }

        var pending = new Queue<string>(desired.Where(entry => !placed.Contains(entry)));
        foreach (var part in parts)
        {
            Fill(part, pending, maxEntries, commands);
        }

        var nextIndex = Math.Max(2, parts.Max(part => part.Index) + 1);
        while (pending.Count > 0)
        {
            var part = new PlannedPart(DnsRoute.PartName(baseName, nextIndex), nextIndex, [], exists: false);
            nextIndex++;
            Fill(part, pending, maxEntries, commands);
            parts.Add(part);
        }

        foreach (var empty in parts.Where(part => part.Index != 1 && part.Entries.Count == 0 && part.Exists).ToList())
        {
            commands.AddRange(state.Routes.Where(rule => rule.Group == empty.Name).Select(rule => DeleteRule(rule.Group, rule.Interface)));
            commands.Add($"no object-group fqdn {empty.Name}");
            parts.Remove(empty);
        }

        return parts;
    }

    private static void Fill(PlannedPart part, Queue<string> pending, int maxEntries, List<string> commands)
    {
        if (!part.Exists)
        {
            commands.Add($"object-group fqdn {part.Name}");
            part.Exists = true;
        }

        while (pending.Count > 0 && part.Entries.Count < maxEntries)
        {
            var entry = pending.Dequeue();
            part.Entries.Add(entry);
            commands.Add($"object-group fqdn {part.Name} include {entry}");
        }
    }

    private static bool PlanRules(
        DnsRouteTarget target,
        List<PlannedPart> parts,
        NdmsDnsState state,
        IReadOnlySet<string> recreatedInterfaces,
        List<string> commands)
    {
        var route = target.Route;
        var interfaceName = target.InterfaceName;
        var previousInterface = route.AppliedInterface is { } applied && !string.Equals(applied, interfaceName, StringComparison.OrdinalIgnoreCase)
            ? applied
            : null;

        var baseRule = state.FindRoute(route.GroupBaseName, interfaceName);
        var previousBaseRule = previousInterface is null ? null : state.FindRoute(route.GroupBaseName, previousInterface);

        var disabled = route.RequestedDisabled
            ?? baseRule?.Disabled
            ?? previousBaseRule?.Disabled
            ?? (!route.Provisioned ? route.LastKnownDisabled
                : recreatedInterfaces.Contains(interfaceName) ? route.LastKnownDisabled
                : true);

        foreach (var part in parts)
        {
            if (previousInterface is not null && state.FindRoute(part.Name, previousInterface) is not null)
            {
                commands.Add(DeleteRule(part.Name, previousInterface));
            }

            var rule = state.FindRoute(part.Name, interfaceName);
            if (rule is not null && (route.RequestedDisabled is null || rule.Disabled == route.RequestedDisabled))
            {
                continue;
            }

            if (rule is not null)
            {
                commands.Add(DeleteRule(part.Name, interfaceName));
            }

            commands.Add($"dns-proxy route object-group {part.Name} {interfaceName} auto");
            if (disabled)
            {
                commands.Add("dns-proxy route disable");
            }
        }

        return disabled;
    }

    private static void RemoveOrphans(NdmsDnsState state, HashSet<string> claimed, List<string> commands)
    {
        var orphanGroups = state.Groups.Values
            .Where(group => group.Name.StartsWith(DnsRoute.GroupPrefix, StringComparison.Ordinal)
                && group.Description.StartsWith(DnsRoute.DescriptionPrefix, StringComparison.Ordinal)
                && !claimed.Contains(group.Name))
            .Select(group => group.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var rule in state.Routes.Where(rule => !claimed.Contains(rule.Group)
            && (orphanGroups.Contains(rule.Group)
                || (rule.Group.StartsWith(DnsRoute.GroupPrefix, StringComparison.Ordinal) && !state.Groups.ContainsKey(rule.Group)))))
        {
            commands.Add(DeleteRule(rule.Group, rule.Interface));
        }

        commands.AddRange(orphanGroups.Order(StringComparer.Ordinal).Select(name => $"no object-group fqdn {name}"));
    }

    private static void RemoveRouteGroups(string baseName, NdmsDnsState state, List<string> commands)
    {
        foreach (var rule in state.Routes.Where(rule => DnsRoute.PartIndex(baseName, rule.Group) is not null))
        {
            commands.Add(DeleteRule(rule.Group, rule.Interface));
        }

        commands.AddRange(ObservedParts(baseName, state).Select(part => $"no object-group fqdn {part.Name}"));
    }

    private static List<(string Name, int Index, IReadOnlyList<string> Entries)> ObservedParts(string baseName, NdmsDnsState state) =>
        state.Groups.Values
            .Select(group => (Group: group, Index: DnsRoute.PartIndex(baseName, group.Name)))
            .Where(item => item.Index is not null)
            .OrderBy(item => item.Index)
            .Select(item => (item.Group.Name, item.Index!.Value, item.Group.Entries))
            .ToList();

    private static string DeleteRule(string group, string interfaceName) =>
        $"no dns-proxy route object-group {group} {interfaceName}";

    private sealed class PlannedPart
    {
        public PlannedPart(string name, int index, List<string> entries, bool exists)
        {
            Name = name;
            Index = index;
            Entries = entries;
            Exists = exists;
        }

        public string Name { get; }

        public int Index { get; }

        public List<string> Entries { get; }

        public bool Exists { get; set; }
    }
}
