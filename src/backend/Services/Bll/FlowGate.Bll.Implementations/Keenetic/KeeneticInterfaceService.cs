using Microsoft.Extensions.Logging;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Contracts.Keenetic;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Keenetic.Models;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Интерфейсы Keenetic через RCI. Изменения идут через <see cref="INdmsOperationQueue"/> и сохраняются одним
///     <c>system configuration save</c> в конце пакета
/// </summary>
public sealed class KeeneticInterfaceService : IKeeneticInterfaceService
{
    private const string InterfacesPath = "show/interface";

    private readonly IKeeneticRciClient _rciClient;
    private readonly INdmsOperationQueue _queue;
    private readonly ILogger<KeeneticInterfaceService> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticInterfaceService"/>
    /// </summary>
    /// <param name="rciClient">Клиент RCI</param>
    /// <param name="queue">Очередь операций NDMS</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public KeeneticInterfaceService(IKeeneticRciClient rciClient, INdmsOperationQueue queue, ILogger<KeeneticInterfaceService> logger)
    {
        _rciClient = rciClient ?? throw new ArgumentNullException(nameof(rciClient));
        _queue = queue ?? throw new ArgumentNullException(nameof(queue));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, KeeneticInterfaceState>> GetAllAsync(CancellationToken cancellationToken) =>
        KeeneticInterfaceParser.ParseAll(await _rciClient.GetAsync(InterfacesPath, cancellationToken));

    /// <inheritdoc />
    public Task ExecuteAsync(string operationName, IReadOnlyList<string> commands, bool ignoreMissing, CancellationToken cancellationToken)
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
                var result = await _rciClient.ExecuteAsync(commands, token);
                result.EnsureSuccess(ignoreMissing ? command => command.HasErrorCode(RciErrorCodes.ArgumentParseError) : null);
                await _rciClient.SaveConfigurationAsync(token);
                _logger.LogDebug("{Operation}: выполнено команд {Count}", operationName, commands.Count);
            },
            cancellationToken);
    }
}
