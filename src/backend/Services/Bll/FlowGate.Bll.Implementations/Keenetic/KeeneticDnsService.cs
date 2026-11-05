using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Keenetic.Models;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Группы и DNS-маршруты Keenetic через RCI. Команды уходят пакетами по <see cref="DomainSourceOptions.RciBatchSize"/>
///     внутри одной операции очереди NDMS, конфигурация сохраняется один раз в конце
/// </summary>
public sealed class KeeneticDnsService : IKeeneticDnsService
{
    private const string GroupsPath = "object-group/fqdn";
    private const string RoutesPath = "dns-proxy/route";

    private readonly IKeeneticRciClient _rciClient;
    private readonly INdmsOperationQueue _queue;
    private readonly int _batchSize;
    private readonly ILogger<KeeneticDnsService> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticDnsService"/>
    /// </summary>
    /// <param name="rciClient">Клиент RCI</param>
    /// <param name="queue">Очередь операций NDMS</param>
    /// <param name="options">Настройки источников (размер пакета)</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public KeeneticDnsService(
        IKeeneticRciClient rciClient,
        INdmsOperationQueue queue,
        IOptions<DomainSourceOptions> options,
        ILogger<KeeneticDnsService> logger)
    {
        _rciClient = rciClient ?? throw new ArgumentNullException(nameof(rciClient));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        ArgumentNullException.ThrowIfNull(options);
        _batchSize = options.Value.RciBatchSize;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<NdmsDnsState> GetStateAsync(CancellationToken cancellationToken)
    {
        var groups = await _rciClient.GetAsync(GroupsPath, cancellationToken);
        var routes = await _rciClient.GetAsync(RoutesPath, cancellationToken);
        return new NdmsDnsState(KeeneticDnsParser.ParseGroups(groups), KeeneticDnsParser.ParseRoutes(routes));
    }

    /// <inheritdoc />
    public Task ExecuteAsync(string operationName, IReadOnlyList<string> commands, Action<string>? report, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return Task.CompletedTask;
        }

        return _queue.EnqueueAsync(
            operationName,
            async token =>
            {
                for (var offset = 0; offset < commands.Count; offset += _batchSize)
                {
                    var batch = commands.Skip(offset).Take(_batchSize).ToList();
                    report?.Invoke($"Команды Keenetic: {offset + batch.Count} из {commands.Count}");
                    var result = await _rciClient.ExecuteAsync(batch, token);
                    result.EnsureSuccess(IsExpected);
                }

                await _rciClient.SaveConfigurationAsync(token);
                _logger.LogDebug("{Operation}: выполнено команд {Count}", operationName, commands.Count);
            },
            cancellationToken);
    }

    private static bool IsExpected(RciCommandResult command) =>
        command.Command.StartsWith("no ", StringComparison.Ordinal) && command.HasErrorCode(RciErrorCodes.ArgumentParseError);
}
