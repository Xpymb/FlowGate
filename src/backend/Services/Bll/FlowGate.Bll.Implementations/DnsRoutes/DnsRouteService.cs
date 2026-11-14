using System.Text;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.DomainSources;
using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Interfaces.DnsRoutes;
using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Contracts.Connections;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Interfaces.Connections;
using FlowGate.Dal.Interfaces.DnsRoutes;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.DnsRoutes;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.Bll.Implementations.DnsRoutes;

/// <summary>
///     DNS-маршруты. Изменения сохраняются сразу, применение в Keenetic — одна фоновая задача на все маршруты: она
///     читает состояние Keenetic, строит план <see cref="DnsRoutePlanner"/> и выполняет его. Просьба применить во время
///     работы задачи не теряется — задача повторяет проход
/// </summary>
public sealed class DnsRouteService : IDnsRouteService, IDisposable
{
    /// <summary>
    ///     Вид фоновой задачи применения
    /// </summary>
    public const string ApplyJobKind = "dns-routes-apply";

    private const string EntityName = "Маршрут";
    private const int MaxSlugLength = 40;

    private readonly IDnsRouteProvider _routes;
    private readonly IDomainSourceProvider _sources;
    private readonly IConnectionProvider _connections;
    private readonly IDomainListReader _reader;
    private readonly IKeeneticDnsService _dns;
    private readonly IJobRunner _jobs;
    private readonly TimeProvider _timeProvider;
    private readonly int _maxEntriesPerRoute;
    private readonly int _maxEntriesPerGroup;
    private readonly ILogger<DnsRouteService> _logger;
    private readonly SemaphoreSlim _applyLock = new(1, 1);
    private readonly Lock _jobSync = new();
    private Guid? _applyJobId;
    private bool _rerunRequested;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DnsRouteService"/>
    /// </summary>
    /// <param name="routes">Хранилище маршрутов</param>
    /// <param name="sources">Хранилище источников</param>
    /// <param name="connections">Хранилище соединений</param>
    /// <param name="reader">Чтение списков</param>
    /// <param name="dns">Группы и правила Keenetic</param>
    /// <param name="jobs">Фоновые задачи</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="sourceOptions">Настройки источников (лимит элементов маршрута)</param>
    /// <param name="keeneticOptions">Настройки Keenetic (лимит элементов группы)</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public DnsRouteService(
        IDnsRouteProvider routes,
        IDomainSourceProvider sources,
        IConnectionProvider connections,
        IDomainListReader reader,
        IKeeneticDnsService dns,
        IJobRunner jobs,
        TimeProvider timeProvider,
        IOptions<DomainSourceOptions> sourceOptions,
        IOptions<KeeneticOptions> keeneticOptions,
        ILogger<DnsRouteService> logger)
    {
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _dns = dns ?? throw new ArgumentNullException(nameof(dns));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(sourceOptions);
        ArgumentNullException.ThrowIfNull(keeneticOptions);
        _maxEntriesPerRoute = sourceOptions.Value.MaxEntriesPerRoute;
        _maxEntriesPerGroup = keeneticOptions.Value.MaxDomainsPerGroup;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DnsRouteDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var (sources, connections) = await LoadReferencesAsync(cancellationToken);
        return (await _routes.GetAllAsync(cancellationToken))
            .Select(route => route.ToDto(sources.GetValueOrDefault(route.SourceId), connections.GetValueOrDefault(route.ConnectionId)))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<DnsRouteDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var route = await GetExistingAsync(id, cancellationToken);
        return await ToDtoAsync(route, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DnsRouteDto> CreateAsync(DnsRouteCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await _sources.GetAsync(request.SourceId ?? string.Empty, cancellationToken)
            ?? throw new RequestValidationException("Выберите существующий источник");
        var listName = request.ListName?.Trim().ToLowerInvariant() ?? string.Empty;
        var filter = DomainSourceService.NormalizeFilter(request.AttributeFilter);
        await EnsureConnectionAsync(request.ConnectionId, cancellationToken);

        var existing = await _routes.GetAllAsync(cancellationToken);
        if (existing.Any(route => route.SourceId == source.Id && route.ListName == listName && route.AttributeFilter == filter))
        {
            throw new ConflictException($"Маршрут для списка «{listName}» этого источника уже есть");
        }

        await EnsureListAsync(source, listName, filter, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        var route = new DnsRoute(
            "rt-" + Guid.NewGuid().ToString("N")[..12],
            source.Id,
            listName,
            filter,
            request.ConnectionId,
            AllocateGroupName(listName, filter, existing),
            Provisioned: false,
            AppliedInterface: null,
            LastKnownDisabled: false,
            RequestedDisabled: null,
            LastApply: null,
            now,
            now)
        {
            Exclusive = request.Exclusive,
        };

        await _routes.AddAsync(route, cancellationToken);
        _logger.LogInformation("Маршрут {RouteId}: список «{List}» ({Source}) → соединение {ConnectionId}", route.Id, listName, source.Name, route.ConnectionId);
        RequestApply();
        return await ToDtoAsync(route, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DnsRouteDto> UpdateAsync(string id, DnsRouteUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await GetExistingAsync(id, cancellationToken);
        var filter = DomainSourceService.NormalizeFilter(request.AttributeFilter);
        await EnsureConnectionAsync(request.ConnectionId, cancellationToken);

        if (filter != current.AttributeFilter && await _sources.GetAsync(current.SourceId, cancellationToken) is { } source)
        {
            await EnsureListAsync(source, current.ListName, filter, cancellationToken);
        }

        var updated = await _routes.UpdateAsync(
            id,
            route => route with
            {
                ConnectionId = request.ConnectionId,
                AttributeFilter = filter,
                RequestedExclusive = request.Exclusive is { } exclusive && exclusive != (route.RequestedExclusive ?? route.Exclusive)
                    ? exclusive
                    : route.RequestedExclusive,
                UpdatedAt = _timeProvider.GetUtcNow(),
            },
            cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

        RequestApply();
        return await ToDtoAsync(updated, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var route = await GetExistingAsync(id, cancellationToken);
        await _routes.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Маршрут {RouteId} списка «{List}» удалён", id, route.ListName);
        RequestApply();
    }

    /// <inheritdoc />
    public async Task<DnsRouteDto> SetDisabledAsync(string id, bool disabled, CancellationToken cancellationToken)
    {
        var updated = await _routes.UpdateAsync(
            id,
            route => route with { RequestedDisabled = disabled, UpdatedAt = _timeProvider.GetUtcNow() },
            cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

        RequestApply();
        return await ToDtoAsync(updated, cancellationToken);
    }

    /// <inheritdoc />
    public JobDto RequestApply()
    {
        lock (_jobSync)
        {
            if (_applyJobId is { } id && _jobs.Get(id) is { Status: JobStatus.Running } running)
            {
                _rerunRequested = true;
                return running.ToDto();
            }

            try
            {
                var job = _jobs.Start(ApplyJobKind, "Синхронизация DNS-маршрутов", RunApplyJobAsync);
                _applyJobId = job.Id;
                return job.ToDto();
            }
            catch (ConflictException)
            {
                _rerunRequested = true;
                return _jobs.List().First(job => job.Kind == ApplyJobKind && job.Status == JobStatus.Running).ToDto();
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> ReconcileAsync(IReadOnlySet<string> recreatedInterfaces, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(recreatedInterfaces);

        if (IsApplyJobRunning() || (await _routes.GetAllAsync(cancellationToken)).Count == 0)
        {
            return -1;
        }

        return await ApplyCoreAsync(recreatedInterfaces, null, cancellationToken);
    }

    /// <summary>
    ///     Выбирает уникальное имя основной группы: <c>fg-&lt;список&gt;[-фильтр]</c>, при совпадении — суффикс
    ///     <c>-r2</c>, <c>-r3</c>, … (не число, чтобы не спутать с частями <c>-2</c>, <c>-3</c>)
    /// </summary>
    /// <param name="listName">Имя списка</param>
    /// <param name="filter">Фильтр или null</param>
    /// <param name="existing">Существующие маршруты</param>
    /// <returns>Имя группы</returns>
    public static string AllocateGroupName(string listName, string? filter, IReadOnlyList<DnsRoute> existing)
    {
        ArgumentNullException.ThrowIfNull(existing);

        var slug = Slug(filter is null ? listName : $"{listName}-{(filter.StartsWith('!') ? "not-" + filter[1..] : filter)}");
        var candidate = DnsRoute.GroupPrefix + slug;
        for (var index = 2; existing.Any(route => Collides(route.GroupBaseName, candidate)); index++)
        {
            candidate = $"{DnsRoute.GroupPrefix}{slug}-r{index}";
        }

        return candidate;
    }

    /// <inheritdoc />
    public void Dispose() => _applyLock.Dispose();

    private async Task RunApplyJobAsync(IJobContext context, CancellationToken cancellationToken)
    {
        bool again;
        do
        {
            lock (_jobSync)
            {
                _rerunRequested = false;
            }

            var commands = await ApplyCoreAsync(new HashSet<string>(StringComparer.OrdinalIgnoreCase), context.Report, cancellationToken);
            context.Report(commands == 0 ? "Изменений нет" : $"Отправлено команд: {commands}");

            lock (_jobSync)
            {
                again = _rerunRequested;
            }
        }
        while (again);
    }

    private async Task<int> ApplyCoreAsync(IReadOnlySet<string> recreatedInterfaces, Action<string>? report, CancellationToken cancellationToken)
    {
        await _applyLock.WaitAsync(cancellationToken);
        try
        {
            var routes = await _routes.GetAllAsync(cancellationToken);
            var (sources, connections) = await LoadReferencesAsync(cancellationToken);
            var targets = new List<DnsRouteTarget>();
            var errors = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var route in routes)
            {
                if (!connections.TryGetValue(route.ConnectionId, out var connection) || !sources.TryGetValue(route.SourceId, out var source))
                {
                    errors[route.Id] = "Соединение или источник маршрута удалены";
                    continue;
                }

                report?.Invoke($"Сборка списка «{route.ListName}»");
                var (content, error) = await ReadContentAsync(source, route, cancellationToken);
                if (error is not null)
                {
                    errors[route.Id] = error;
                }

                targets.Add(new DnsRouteTarget(route, connection.InterfaceName, Describe(route, source), content));
            }

            report?.Invoke("Чтение групп и маршрутов Keenetic");
            var state = await _dns.GetStateAsync(cancellationToken);
            var plan = DnsRoutePlanner.Plan(targets, state, recreatedInterfaces, _maxEntriesPerGroup);
            var warnings = FindOverlaps(targets);

            try
            {
                await _dns.ExecuteAsync("Применение DNS-маршрутов", plan.Commands, report, cancellationToken);
            }
            catch (Exception ex) when (ex is KeeneticCommandException or KeeneticUnavailableException)
            {
                foreach (var route in routes)
                {
                    await RecordAsync(route.Id, route => route with { LastApply = Apply(DnsRouteApplyStatus.Failed, ex.Message, route.LastApply) }, CancellationToken.None);
                }

                throw;
            }

            await RecordOutcomesAsync(plan, targets, errors, warnings, cancellationToken);
            return plan.Commands.Count;
        }
        finally
        {
            _applyLock.Release();
        }
    }

    private async Task<(ListContent? Content, string? Error)> ReadContentAsync(DomainSource source, DnsRoute route, CancellationToken cancellationToken)
    {
        try
        {
            var content = await _reader.ReadAsync(source, route.ListName, route.AttributeFilter, cancellationToken);
            return content.Entries.Count > _maxEntriesPerRoute
                ? (null, $"В списке {content.Entries.Count} элементов, больше допустимых {_maxEntriesPerRoute}")
                : (content, null);
        }
        catch (DomainListException ex)
        {
            return (null, ex.Message);
        }
    }

    private async Task RecordOutcomesAsync(
        DnsRoutePlan plan,
        IReadOnlyList<DnsRouteTarget> targets,
        IReadOnlyDictionary<string, string> errors,
        IReadOnlyDictionary<string, IReadOnlyList<string>> warnings,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        foreach (var outcome in plan.Outcomes)
        {
            var target = targets.First(item => item.Route.Id == outcome.RouteId);
            var error = errors.GetValueOrDefault(outcome.RouteId);
            switch (outcome.Kind)
            {
                case DnsRouteOutcomeKind.RemovedByUser:
                {
                    await _routes.DeleteAsync(outcome.RouteId, cancellationToken);
                    _logger.LogInformation(
                        "Список «{List}» удалён в Keenetic пользователем: маршрут {RouteId} удалён из FlowGate",
                        target.Route.ListName,
                        outcome.RouteId);
                    break;
                }
                case DnsRouteOutcomeKind.Waiting:
                {
                    await RecordAsync(outcome.RouteId, route => route with { LastApply = Apply(DnsRouteApplyStatus.Waiting, error, route.LastApply) }, cancellationToken);
                    break;
                }
                default:
                {
                    var apply = new DnsRouteApply(
                        now,
                        error is null ? DnsRouteApplyStatus.Ok : DnsRouteApplyStatus.Waiting,
                        error,
                        outcome.Entries,
                        target.Content?.Skipped ?? 0,
                        outcome.Parts,
                        warnings.GetValueOrDefault(outcome.RouteId) ?? []);
                    await RecordAsync(
                        outcome.RouteId,
                        route => route with
                        {
                            Provisioned = true,
                            AppliedInterface = target.InterfaceName,
                            LastKnownDisabled = outcome.Disabled,
                            RequestedDisabled = null,
                            Exclusive = outcome.Exclusive,
                            RequestedExclusive = null,
                            LastApply = apply with
                            {
                                ContentChangedAt = outcome.ContentChanged ? now : route.LastApply?.ContentChangedAt,
                            },
                        },
                        cancellationToken);
                    break;
                }
            }
        }

        foreach (var (routeId, error) in errors.Where(pair => plan.Outcomes.All(outcome => outcome.RouteId != pair.Key)))
        {
            await RecordAsync(routeId, route => route with { LastApply = Apply(DnsRouteApplyStatus.Failed, error, route.LastApply) }, cancellationToken);
        }
    }

    private async Task RecordAsync(string id, Func<DnsRoute, DnsRoute> update, CancellationToken cancellationToken) =>
        await _routes.UpdateAsync(id, update, cancellationToken);

    private DnsRouteApply Apply(DnsRouteApplyStatus status, string? error, DnsRouteApply? previous) =>
        new(
            _timeProvider.GetUtcNow(),
            status,
            error,
            previous?.Entries ?? 0,
            previous?.Skipped ?? 0,
            previous?.Parts ?? 0,
            previous?.Warnings ?? [],
            previous?.ContentChangedAt);

    private static Dictionary<string, IReadOnlyList<string>> FindOverlaps(IReadOnlyList<DnsRouteTarget> targets)
    {
        var owners = new Dictionary<string, List<DnsRouteTarget>>(StringComparer.Ordinal);
        foreach (var target in targets.Where(target => target.Content is not null))
        {
            foreach (var entry in target.Content!.Entries)
            {
                if (!owners.TryGetValue(entry, out var list))
                {
                    owners[entry] = list = [];
                }

                list.Add(target);
            }
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var target in targets.Where(target => target.Content is not null))
        {
            var conflicts = target.Content!.Entries
                .SelectMany(entry => owners[entry].Where(other => other.Route.ConnectionId != target.Route.ConnectionId))
                .GroupBy(other => other.Route.Id)
                .Select(group => $"{group.Count()} элементов есть и в маршруте «{group.First().Route.ListName}» на другое соединение")
                .ToList();
            if (conflicts.Count > 0)
            {
                result[target.Route.Id] = conflicts;
            }
        }

        return result;
    }

    private async Task EnsureListAsync(DomainSource source, string listName, string? filter, CancellationToken cancellationToken)
    {
        if (!V2flyListParser.IsValidListName(listName))
        {
            throw new RequestValidationException("Некорректное имя списка");
        }

        if (await _reader.GetCatalogAsync(source, cancellationToken) is null)
        {
            throw new RequestValidationException($"Источник «{source.Name}» ещё не загружен: обновите его и дождитесь окончания");
        }

        ListContent content;
        try
        {
            content = await _reader.ReadAsync(source, listName, filter, cancellationToken);
        }
        catch (DomainListException ex)
        {
            throw new RequestValidationException(ex.Message);
        }

        if (content.Entries.Count > _maxEntriesPerRoute)
        {
            throw new RequestValidationException($"В списке {content.Entries.Count} элементов, больше допустимых {_maxEntriesPerRoute}: сузьте его фильтром");
        }
    }

    private async Task EnsureConnectionAsync(string? connectionId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connectionId) || await _connections.GetAsync(connectionId, cancellationToken) is null)
        {
            throw new RequestValidationException("Выберите существующее соединение");
        }
    }

    private bool IsApplyJobRunning()
    {
        lock (_jobSync)
        {
            return _applyJobId is { } id && _jobs.Get(id) is { Status: JobStatus.Running };
        }
    }

    private async Task<(Dictionary<string, DomainSource> Sources, Dictionary<string, Connection> Connections)> LoadReferencesAsync(CancellationToken cancellationToken) =>
        ((await _sources.GetAllAsync(cancellationToken)).ToDictionary(source => source.Id, StringComparer.Ordinal),
            (await _connections.GetAllAsync(cancellationToken)).ToDictionary(connection => connection.Id, StringComparer.Ordinal));

    private async Task<DnsRouteDto> ToDtoAsync(DnsRoute route, CancellationToken cancellationToken) =>
        route.ToDto(await _sources.GetAsync(route.SourceId, cancellationToken), await _connections.GetAsync(route.ConnectionId, cancellationToken));

    private async Task<DnsRoute> GetExistingAsync(string id, CancellationToken cancellationToken) =>
        await _routes.GetAsync(id, cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

    private static string Describe(DnsRoute route, DomainSource source)
    {
        var filter = route.AttributeFilter is null ? string.Empty : $" @{route.AttributeFilter}";
        return $"{DnsRoute.DescriptionPrefix}{route.ListName}{filter} ({source.Name})".Replace('"', '\'');
    }

    private static bool Collides(string first, string second) =>
        DnsRoute.PartIndex(first, second) is not null || DnsRoute.PartIndex(second, first) is not null;

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            var mapped = char.IsAsciiLetterOrDigit(character) ? character : '-';
            if (mapped != '-' || (builder.Length > 0 && builder[^1] != '-'))
            {
                builder.Append(mapped);
            }
        }

        var slug = builder.ToString().Trim('-');
        slug = slug.Length > MaxSlugLength ? slug[..MaxSlugLength].TrimEnd('-') : slug;
        return slug.Length == 0 ? "list" : slug;
    }
}
