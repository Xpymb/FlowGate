using System.Text.RegularExpressions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.DomainSources.Readers;
using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Settings;
using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Interfaces.DnsRoutes;
using FlowGate.Bll.Interfaces.DomainSources;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Dal.Interfaces.DnsRoutes;
using FlowGate.Dal.Interfaces.DomainSources;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.DomainSources;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.DomainSources;

/// <summary>
///     Источники списков: хранение, загрузка через фоновые задачи, каталог, предпросмотр, локальные списки
/// </summary>
public sealed partial class DomainSourceService : IDomainSourceService
{
    private const string EntityName = "Источник";
    private const string JobKindPrefix = "domain-source-refresh:";
    private const int MaxNameLength = 64;
    private const int MaxLocalListLength = 1024 * 1024;
    private const int PreviewEntries = 500;
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

    private readonly IDomainSourceProvider _sources;
    private readonly ILocalListProvider _localLists;
    private readonly IDomainSourceFileProvider _files;
    private readonly IDomainListReader _reader;
    private readonly IDomainSourceDownloader _downloader;
    private readonly IDnsRouteProvider _routes;
    private readonly IDnsRouteService _routeService;
    private readonly IJobRunner _jobs;
    private readonly TimeProvider _timeProvider;
    private readonly ISettingsProvider _settings;
    private readonly IUpdateProxyService _updateProxy;
    private readonly DomainSourceOptions _options;
    private readonly ILogger<DomainSourceService> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainSourceService"/>
    /// </summary>
    /// <param name="sources">Хранилище источников</param>
    /// <param name="localLists">Хранилище локальных списков</param>
    /// <param name="files">Файлы источников</param>
    /// <param name="reader">Чтение списков</param>
    /// <param name="downloader">Загрузка источников</param>
    /// <param name="routes">Хранилище маршрутов (проверка использования)</param>
    /// <param name="routeService">Маршруты (применение после обновления)</param>
    /// <param name="jobs">Фоновые задачи</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="settings">Пользовательские настройки (интервал обновления)</param>
    /// <param name="updateProxy">Прокси обновлений</param>
    /// <param name="options">Настройки источников</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public DomainSourceService(
        IDomainSourceProvider sources,
        ILocalListProvider localLists,
        IDomainSourceFileProvider files,
        IDomainListReader reader,
        IDomainSourceDownloader downloader,
        IDnsRouteProvider routes,
        IDnsRouteService routeService,
        IJobRunner jobs,
        TimeProvider timeProvider,
        ISettingsProvider settings,
        IUpdateProxyService updateProxy,
        IOptions<DomainSourceOptions> options,
        ILogger<DomainSourceService> logger)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _localLists = localLists ?? throw new ArgumentNullException(nameof(localLists));
        _files = files ?? throw new ArgumentNullException(nameof(files));
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _routeService = routeService ?? throw new ArgumentNullException(nameof(routeService));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        ArgumentNullException.ThrowIfNull(options);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _updateProxy = updateProxy ?? throw new ArgumentNullException(nameof(updateProxy));
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainSourceDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var routes = await _routes.GetAllAsync(cancellationToken);
        var result = new List<DomainSourceDto>();
        foreach (var source in await _sources.GetAllAsync(cancellationToken))
        {
            result.Add(await ToDtoAsync(source, routes.Count(route => route.SourceId == source.Id), cancellationToken));
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<DomainSourceDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        var routes = await _routes.GetAllAsync(cancellationToken);
        return await ToDtoAsync(source, routes.Count(route => route.SourceId == id), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DomainSourceDto> CreateAsync(DomainSourceSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var kind = ParseKind(request.Kind);
        var (repository, branch, dat) = ValidateOrigin(kind, request);
        var source = new DomainSource(
            "src-" + Guid.NewGuid().ToString("N")[..12],
            ValidateName(request.Name),
            kind,
            repository,
            branch,
            dat,
            request.AutoUpdate,
            BuiltIn: false,
            null,
            null,
            _timeProvider.GetUtcNow());

        await _sources.AddAsync(source, cancellationToken);
        _logger.LogInformation("Источник {SourceId} «{Name}» добавлен ({Origin})", source.Id, source.Name, source.SafeOrigin);

        if (source.HasFile)
        {
            StartRefresh(source);
        }
        else
        {
            await _reader.BuildCatalogAsync(source, null, cancellationToken);
        }

        return await ToDtoAsync(source, 0, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DomainSourceDto> UpdateAsync(string id, DomainSourceSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await GetExistingAsync(id, cancellationToken);
        if (ParseKind(request.Kind) != current.Kind)
        {
            throw new RequestValidationException("Вид источника не меняется: создайте новый источник");
        }

        var name = ValidateName(request.Name);
        var (repository, branch, dat) = current.BuiltIn
            ? (current.Repository, current.Branch, current.Dat)
            : ValidateOrigin(current.Kind, request);
        var originChanged = repository != current.Repository || branch != current.Branch || dat != current.Dat;

        var updated = await _sources.UpdateAsync(
            id,
            existing => existing with
            {
                Name = name,
                AutoUpdate = request.AutoUpdate,
                Repository = repository,
                Branch = branch,
                Dat = dat,
                Downloaded = originChanged ? null : existing.Downloaded,
            },
            cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

        if (originChanged && updated.HasFile)
        {
            StartRefresh(updated);
        }

        _logger.LogInformation("Источник {SourceId} «{Name}» изменён", id, updated.Name);
        return await GetAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        if (source.BuiltIn)
        {
            throw new RequestValidationException("Встроенный источник удалить нельзя; можно выключить его автообновление");
        }

        var used = (await _routes.GetAllAsync(cancellationToken)).Where(route => route.SourceId == id).Select(route => $"«{route.ListName}»").ToList();
        if (used.Count > 0)
        {
            throw new ConflictException($"Списки источника используются в маршрутах {string.Join(", ", used)}: сначала удалите их");
        }

        await _sources.DeleteAsync(id, cancellationToken);
        await _localLists.DeleteSourceAsync(id, cancellationToken);
        _files.Delete(id);
        _reader.Invalidate(id);
        _logger.LogInformation("Источник {SourceId} «{Name}» удалён", id, source.Name);
    }

    /// <inheritdoc />
    public async Task<JobDto> RefreshAsync(string id, UpdateVia via, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        return StartRefresh(source, via).ToDto();
    }

    /// <inheritdoc />
    public async Task<int> RefreshDueAsync(CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var refreshInterval = UpdateIntervals.DomainSourceRefresh(await _settings.GetAsync(cancellationToken), _options);
        var used = (await _routes.GetAllAsync(cancellationToken)).Select(route => route.SourceId).ToHashSet(StringComparer.Ordinal);
        var due = (await _sources.GetAllAsync(cancellationToken))
            .Where(source => source.HasFile && source.AutoUpdate && used.Contains(source.Id))
            .Where(source => source.LastRefresh is null
                || source.LastRefresh.At + (source.LastRefresh.Status == DomainSourceRefreshStatus.Failed ? RetryAfterFailure : refreshInterval) <= now)
            .ToList();

        var refreshed = 0;
        foreach (var source in due)
        {
            JobSnapshot job;
            try
            {
                job = StartRefresh(source);
            }
            catch (ConflictException)
            {
                continue;
            }

            await _jobs.WaitAsync(job.Id, cancellationToken);
            refreshed++;
        }

        return refreshed;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DomainListSummaryDto>> GetListsAsync(string id, string? query, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        var catalog = await _reader.GetCatalogAsync(source, cancellationToken);
        var filter = query?.Trim();

        return catalog is null
            ? []
            : catalog.Lists
                .Where(list => string.IsNullOrEmpty(filter) || list.Name.Contains(filter, StringComparison.OrdinalIgnoreCase))
                .Select(list => list.ToDto())
                .ToList();
    }

    /// <inheritdoc />
    public async Task<DomainListPreviewDto> PreviewAsync(string id, string name, string? attributeFilter, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        var filter = NormalizeFilter(attributeFilter);

        ListContent content;
        try
        {
            content = await _reader.ReadAsync(source, name.Trim().ToLowerInvariant(), filter, cancellationToken);
        }
        catch (DomainListException ex)
        {
            throw new RequestValidationException(ex.Message);
        }

        var (domains, ipv4, ipv6) = content.CountByKind();
        return new DomainListPreviewDto(
            name,
            filter,
            content.Entries.Count,
            domains,
            ipv4,
            ipv6,
            content.SkippedKeyword,
            content.SkippedRegexp,
            content.SkippedInvalid,
            content.Entries.Take(PreviewEntries).ToList(),
            content.Entries.Count > PreviewEntries);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<LocalListDto>> GetLocalListsAsync(string id, CancellationToken cancellationToken)
    {
        var source = await GetLocalSourceAsync(id, cancellationToken);
        return (await _localLists.GetAllAsync(source.Id, cancellationToken)).Select(list => list.ToDto()).ToList();
    }

    /// <inheritdoc />
    public async Task<LocalListDto> SaveLocalListAsync(string id, string name, LocalListSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var source = await GetLocalSourceAsync(id, cancellationToken);
        var listName = name?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!V2flyListParser.IsValidListName(listName))
        {
            throw new RequestValidationException("Имя списка: латиница в нижнем регистре, цифры и -._!, до 64 символов");
        }

        var text = request.Text ?? string.Empty;
        if (text.Length > MaxLocalListLength)
        {
            throw new RequestValidationException($"Список больше {MaxLocalListLength / 1024} КиБ");
        }

        var parsed = V2flyListParser.Parse(text);
        if (parsed.Errors.Count > 0)
        {
            throw new RequestValidationException("Список содержит ошибки", parsed.Errors.Select(error => error.ToString()).ToList());
        }

        var others = (await _localLists.GetAllAsync(source.Id, cancellationToken))
            .Where(list => list.Name != listName)
            .Select(list => (list.Name, list.Text));
        try
        {
            V2flyTextLists.Parse([.. others, (listName, text)], string.Empty).Resolve(listName, null);
        }
        catch (DomainListException ex)
        {
            throw new RequestValidationException(ex.Message);
        }

        var list = new LocalList(source.Id, listName, text, _timeProvider.GetUtcNow());
        await _localLists.UpsertAsync(list, cancellationToken);
        await RebuildLocalCatalogAsync(source, cancellationToken);
        _logger.LogInformation("Локальный список «{List}» источника «{Source}» сохранён", listName, source.Name);
        return list.ToDto();
    }

    /// <inheritdoc />
    public async Task DeleteLocalListAsync(string id, string name, CancellationToken cancellationToken)
    {
        var source = await GetLocalSourceAsync(id, cancellationToken);
        var listName = name?.Trim().ToLowerInvariant() ?? string.Empty;
        if ((await _routes.GetAllAsync(cancellationToken)).Any(route => route.SourceId == id && route.ListName == listName))
        {
            throw new ConflictException($"Список «{listName}» используется в маршруте: сначала удалите маршрут");
        }

        if (!await _localLists.DeleteAsync(source.Id, listName, cancellationToken))
        {
            throw new EntityNotFoundException("Список", listName);
        }

        await RebuildLocalCatalogAsync(source, cancellationToken);
    }

    /// <summary>
    ///     Нормализует фильтр атрибута
    /// </summary>
    /// <param name="attributeFilter">Фильтр из запроса</param>
    /// <returns>Фильтр в нижнем регистре или null</returns>
    /// <exception cref="RequestValidationException">Если фильтр некорректен</exception>
    public static string? NormalizeFilter(string? attributeFilter)
    {
        var filter = attributeFilter?.Trim().TrimStart('@').ToLowerInvariant();
        if (string.IsNullOrEmpty(filter))
        {
            return null;
        }

        return V2flyListParser.IsValidAttributeFilter(filter)
            ? filter
            : throw new RequestValidationException("Фильтр атрибута: имя вроде ads или !ads");
    }

    private JobSnapshot StartRefresh(DomainSource source, UpdateVia via = UpdateVia.Auto) =>
        _jobs.Start(
            JobKindPrefix + source.Id,
            $"Обновление источника «{source.Name}»",
            (context, token) => UpdateRoute.RunAsync(_updateProxy, via, context, () => RefreshCoreAsync(source.Id, context, token), token));

    private async Task RefreshCoreAsync(string id, IJobContext context, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        if (!source.HasFile)
        {
            await RebuildLocalCatalogAsync(source, cancellationToken);
            return;
        }

        var current = _files.GetFilePath(id);
        var candidate = _files.CreateCandidatePath(id);
        try
        {
            var revision = await _downloader.DownloadAsync(source, current is not null, candidate, context.Report, cancellationToken);
            var contentChanged = revision is not null && (current is null || revision.Sha256 != source.Downloaded?.Sha256);
            SourceListCatalog catalog;

            if (contentChanged)
            {
                context.Report("Разбор списков");
                catalog = await _reader.BuildCatalogAsync(source, candidate, cancellationToken);
                _files.Commit(id, candidate);
            }
            else
            {
                catalog = await _reader.GetCatalogAsync(source, cancellationToken)
                    ?? await _reader.BuildCatalogAsync(source, current, cancellationToken);
            }

            var refresh = new DomainSourceRefresh(
                _timeProvider.GetUtcNow(),
                catalog.Errors.Count > 0 ? DomainSourceRefreshStatus.Partial : DomainSourceRefreshStatus.Ok,
                null,
                catalog.Lists.Count,
                catalog.Errors.Take(50).ToList());
            await _sources.UpdateAsync(id, existing => existing with { Downloaded = revision ?? existing.Downloaded, LastRefresh = refresh }, cancellationToken);

            var summary = contentChanged ? $"обновлён, списков {catalog.Lists.Count}" : "не изменился";
            context.Report($"Готово: {summary}");
            _logger.LogInformation("Источник {SourceId} «{Name}» ({Origin}) {Summary}", id, source.Name, source.SafeOrigin, summary);

            if (contentChanged && (await _routes.GetAllAsync(cancellationToken)).Any(route => route.SourceId == id))
            {
                _routeService.RequestApply();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var message = ex is DomainListException or FlowGateException ? ex.Message : $"Ошибка обновления: {ex.Message}";
            await _sources.UpdateAsync(
                id,
                existing => existing with { LastRefresh = new DomainSourceRefresh(_timeProvider.GetUtcNow(), DomainSourceRefreshStatus.Failed, message, existing.LastRefresh?.Lists ?? 0, []) },
                CancellationToken.None);
            _logger.LogWarning("Источник {SourceId} ({Origin}) не обновлён: {Error}", id, source.SafeOrigin, message);
            if (ex is DomainListException)
            {
                throw new ExternalServiceUnavailableException(message);
            }

            throw;
        }
        finally
        {
            _files.Discard(candidate);
            ReleaseParsingMemory();
        }
    }

    private static void ReleaseParsingMemory() =>
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

    private async Task RebuildLocalCatalogAsync(DomainSource source, CancellationToken cancellationToken)
    {
        var catalog = await _reader.BuildCatalogAsync(source, null, cancellationToken);
        await _sources.UpdateAsync(
            source.Id,
            existing => existing with
            {
                LastRefresh = new DomainSourceRefresh(
                    _timeProvider.GetUtcNow(),
                    catalog.Errors.Count > 0 ? DomainSourceRefreshStatus.Partial : DomainSourceRefreshStatus.Ok,
                    null,
                    catalog.Lists.Count,
                    catalog.Errors.Take(50).ToList()),
            },
            cancellationToken);

        if ((await _routes.GetAllAsync(cancellationToken)).Any(route => route.SourceId == source.Id))
        {
            _routeService.RequestApply();
        }
    }

    private async Task<DomainSourceDto> ToDtoAsync(DomainSource source, int routeCount, CancellationToken cancellationToken)
    {
        var catalog = await _reader.GetCatalogAsync(source, cancellationToken);
        var downloaded = !source.HasFile || _files.GetFilePath(source.Id) is not null;
        return source.ToDto(downloaded, catalog?.Lists.Count ?? 0, routeCount);
    }

    private async Task<DomainSource> GetExistingAsync(string id, CancellationToken cancellationToken) =>
        await _sources.GetAsync(id, cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

    private async Task<DomainSource> GetLocalSourceAsync(string id, CancellationToken cancellationToken)
    {
        var source = await GetExistingAsync(id, cancellationToken);
        return source.Kind == DomainSourceKind.Local
            ? source
            : throw new RequestValidationException("Списки правятся только у локального источника");
    }

    private static string ValidateName(string? requested)
    {
        var name = requested?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxNameLength || name.Any(character => character is '"' or '\\' || char.IsControl(character)))
        {
            throw new RequestValidationException($"Имя источника обязательно, до {MaxNameLength} символов, без кавычек");
        }

        return name;
    }

    private static DomainSourceKind ParseKind(string? kind) =>
        kind?.Trim().ToLowerInvariant() switch
        {
            "githubrepository" or "github" or "repository" => DomainSourceKind.GitHubRepository,
            "geositedat" or "geosite" => DomainSourceKind.GeositeDat,
            "geoipdat" or "geoip" => DomainSourceKind.GeoipDat,
            "local" => DomainSourceKind.Local,
            _ => throw new RequestValidationException("Вид источника: GitHubRepository, GeositeDat, GeoipDat или Local"),
        };

    private static (string? Repository, string? Branch, DatOrigin? Dat) ValidateOrigin(DomainSourceKind kind, DomainSourceSaveRequest request)
    {
        switch (kind)
        {
            case DomainSourceKind.GitHubRepository:
            {
                var repository = ValidateRepository(request.Repository);
                var branch = string.IsNullOrWhiteSpace(request.Branch) ? null : request.Branch.Trim();
                if (branch is not null && !BranchRegex().IsMatch(branch))
                {
                    throw new RequestValidationException("Некорректное имя ветки");
                }

                return (repository, branch, null);
            }
            case DomainSourceKind.GeositeDat or DomainSourceKind.GeoipDat:
            {
                if (!string.IsNullOrWhiteSpace(request.Url))
                {
                    return (null, null, DatOrigin.FromUrl(ValidateUrl(request.Url), string.IsNullOrWhiteSpace(request.ChecksumUrl) ? null : ValidateUrl(request.ChecksumUrl)));
                }

                var asset = request.Asset?.Trim();
                if (string.IsNullOrEmpty(asset) || !AssetRegex().IsMatch(asset))
                {
                    throw new RequestValidationException("Укажите ссылку на файл или репозиторий релизов и имя файла в релизе");
                }

                var checksum = string.IsNullOrWhiteSpace(request.ChecksumAsset) ? null : request.ChecksumAsset.Trim();
                if (checksum is not null && !AssetRegex().IsMatch(checksum))
                {
                    throw new RequestValidationException("Некорректное имя файла контрольной суммы");
                }

                return (null, null, DatOrigin.FromRelease(ValidateRepository(request.ReleaseRepository), asset, checksum));
            }
            default:
            {
                return (null, null, null);
            }
        }
    }

    private static string ValidateRepository(string? repository)
    {
        var value = repository?.Trim().Trim('/') ?? string.Empty;
        if (value.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            value = value["https://github.com/".Length..].Trim('/');
        }

        return RepositoryRegex().IsMatch(value)
            ? value
            : throw new RequestValidationException("Репозиторий GitHub в виде owner/repo");
    }

    private static string ValidateUrl(string url)
    {
        var value = url.Trim();
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? value
            : throw new RequestValidationException("Ссылка должна быть http(s)-адресом");
    }

    [GeneratedRegex("^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    private static partial Regex RepositoryRegex();

    [GeneratedRegex("^[A-Za-z0-9._/-]{1,100}$")]
    private static partial Regex BranchRegex();

    [GeneratedRegex("^[A-Za-z0-9._+-]{1,100}$")]
    private static partial Regex AssetRegex();
}
