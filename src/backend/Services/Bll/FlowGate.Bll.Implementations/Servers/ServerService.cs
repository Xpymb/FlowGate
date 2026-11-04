using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;

using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Contracts.Servers;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Серверы для API поверх хранилища, разбора ссылок и проверки outbound
/// </summary>
public sealed class ServerService : IServerService
{
    private const int MaxNameLength = 128;
    private const string EntityName = "Сервер";

    private readonly IServerProvider _servers;
    private readonly IServerImportParser _parser;
    private readonly IOutboundValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerService> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerService"/>
    /// </summary>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="parser">Разбор ссылок и Xray-JSON</param>
    /// <param name="validator">Проверка outbound</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public ServerService(
        IServerProvider servers,
        IServerImportParser parser,
        IOutboundValidator validator,
        TimeProvider timeProvider,
        ILogger<ServerService> logger)
    {
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ServerDto>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _servers.GetAllAsync(cancellationToken)).Select(server => server.ToDto()).ToList();

    /// <inheritdoc />
    public async Task<ServerDetailsDto> GetAsync(string id, CancellationToken cancellationToken) =>
        (await GetExistingAsync(id, cancellationToken)).ToDetailsDto();

    /// <inheritdoc />
    public ServerDraftDto Parse(ServerParseRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new RequestValidationException("Вставьте ссылку на сервер или Xray-JSON");
        }

        var batch = _parser.Parse(request.Text);
        if (batch.Servers.Count == 0)
        {
            var reasons = batch.Rejected.Select(rejection => rejection.Reason).ToList();
            throw new RequestValidationException(
                reasons.Count == 0 ? "Не найдено ни одной ссылки на сервер" : "Не удалось разобрать сервер",
                reasons);
        }

        var warnings = batch.Warnings.ToList();
        if (batch.Servers.Count > 1)
        {
            warnings.Add($"Найдено серверов: {batch.Servers.Count}, показан первый. Для добавления всех используйте импорт");
        }

        var first = batch.Servers[0];
        return new ServerDraftDto(first.Name, first.Outbound, warnings);
    }

    /// <inheritdoc />
    public async Task<ServerValidationDto> ValidateAsync(ServerValidateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Outbound is null)
        {
            throw new RequestValidationException("Нужен outbound");
        }

        var result = await _validator.ValidateAsync(WithoutTag(request.Outbound), cancellationToken);
        return new ServerValidationDto(result.IsValid, result.CheckedByXray, result.Errors);
    }

    /// <inheritdoc />
    public async Task<ServerDetailsDto> CreateAsync(ServerSaveRequest request, CancellationToken cancellationToken)
    {
        var (name, outbound) = await PrepareAsync(request, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var server = new Server(NewId(), name, outbound, ServerSourceKind.Manual, null, ServerImportKey.Compute(outbound), now, now);

        await _servers.AddAsync([server], cancellationToken);
        _logger.LogInformation("Сервер {ServerId} «{Name}» добавлен", server.Id, server.Name);
        return server.ToDetailsDto();
    }

    /// <inheritdoc />
    public async Task<ServerDetailsDto> UpdateAsync(string id, ServerSaveRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetExistingAsync(id, cancellationToken);
        EnsureManual(existing, "изменить");

        var (name, outbound) = await PrepareAsync(request, cancellationToken);
        var updated = existing with
        {
            Name = name,
            Outbound = outbound,
            ImportKey = ServerImportKey.Compute(outbound),
            UpdatedAt = _timeProvider.GetUtcNow(),
        };

        if (!await _servers.UpdateAsync(updated, cancellationToken))
        {
            throw new EntityNotFoundException(EntityName, id);
        }

        _logger.LogInformation("Сервер {ServerId} «{Name}» изменён", updated.Id, updated.Name);
        return updated.ToDetailsDto();
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string id, CancellationToken cancellationToken)
    {
        var existing = await GetExistingAsync(id, cancellationToken);
        EnsureManual(existing, "удалить");

        await _servers.DeleteAsync(id, cancellationToken);
        _logger.LogInformation("Сервер {ServerId} «{Name}» удалён", existing.Id, existing.Name);
    }

    /// <inheritdoc />
    public async Task<ServerImportResultDto> ImportAsync(ServerImportRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new RequestValidationException("Вставьте ссылки на серверы или Xray-JSON");
        }

        var batch = _parser.Parse(request.Text);
        var existingKeys = (await _servers.GetAllAsync(cancellationToken))
            .Where(server => server.SourceKind == ServerSourceKind.Manual)
            .Select(server => server.ImportKey)
            .ToHashSet(StringComparer.Ordinal);

        var now = _timeProvider.GetUtcNow();
        var added = new List<Server>();
        var skipped = 0;
        foreach (var parsed in batch.Servers)
        {
            var key = ServerImportKey.Compute(parsed.Outbound);
            if (!existingKeys.Add(key))
            {
                skipped++;
                continue;
            }

            added.Add(new Server(NewId(), parsed.Name, parsed.Outbound, ServerSourceKind.Manual, null, key, now, now));
        }

        await _servers.AddAsync(added, cancellationToken);
        _logger.LogInformation("Импорт серверов: добавлено {Added}, пропущено {Skipped}, отклонено {Rejected}", added.Count, skipped, batch.Rejected.Count);

        return new ServerImportResultDto(
            added.Select(server => server.ToDto()).ToList(),
            skipped,
            batch.Rejected.Select(rejection => new ServerImportRejectionDto(rejection.Index, rejection.Reason)).ToList(),
            batch.Warnings);
    }

    private async Task<(string Name, JsonObject Outbound)> PrepareAsync(ServerSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is 0 or > MaxNameLength)
        {
            throw new RequestValidationException($"Имя сервера обязательно, не длиннее {MaxNameLength} символов");
        }

        if (request.Outbound is null)
        {
            throw new RequestValidationException("Нужен outbound");
        }

        var outbound = WithoutTag(request.Outbound);
        var validation = await _validator.ValidateAsync(outbound, cancellationToken);
        if (!validation.IsValid)
        {
            throw new RequestValidationException("Outbound не прошёл проверку", validation.Errors);
        }

        return (name, outbound);
    }

    private async Task<Server> GetExistingAsync(string id, CancellationToken cancellationToken) =>
        await _servers.GetAsync(id, cancellationToken) ?? throw new EntityNotFoundException(EntityName, id);

    private static void EnsureManual(Server server, string action)
    {
        if (server.SourceKind != ServerSourceKind.Manual)
        {
            throw new RequestValidationException($"Сервер из подписки нельзя {action}: он перезаписывается при обновлении подписки");
        }
    }

    private static JsonObject WithoutTag(JsonObject outbound)
    {
        var copy = (JsonObject)outbound.DeepClone();
        copy.Remove("tag");
        return copy;
    }

    private static string NewId() => "srv-" + Guid.NewGuid().ToString("N")[..12];
}
