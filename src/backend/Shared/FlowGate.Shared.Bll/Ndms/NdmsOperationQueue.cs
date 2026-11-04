using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Exceptions;
using FlowGate.Shared.Options;

namespace FlowGate.Shared.Bll.Ndms;

/// <summary>
///     Очередь операций NDMS на основе семафора с одним слотом
/// </summary>
public sealed class NdmsOperationQueue : INdmsOperationQueue, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger<NdmsOperationQueue> _logger;
    private readonly TimeSpan _timeout;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="NdmsOperationQueue"/>
    /// </summary>
    /// <param name="options">Настройки Keenetic (таймаут операции)</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если options или logger равны null</exception>
    public NdmsOperationQueue(IOptions<KeeneticOptions> options, ILogger<NdmsOperationQueue> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = TimeSpan.FromSeconds(options.Value.OperationTimeoutSeconds);
    }

    /// <inheritdoc />
    public async Task<T> EnqueueAsync<T>(
        string operationName,
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentNullException.ThrowIfNull(operation);

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(_timeout);

        var waiting = Stopwatch.StartNew();
        try
        {
            await _gate.WaitAsync(timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw TimeoutError(operationName, "в ожидании очереди");
        }

        var running = Stopwatch.StartNew();
        try
        {
            var result = await operation(timeoutSource.Token);
            _logger.LogInformation(
                "NDMS: «{Operation}» выполнена за {ElapsedMs} мс (ожидание очереди {WaitMs} мс)",
                operationName,
                running.ElapsedMilliseconds,
                waiting.ElapsedMilliseconds - running.ElapsedMilliseconds);
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && timeoutSource.IsCancellationRequested)
        {
            throw TimeoutError(operationName, "во время выполнения");
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public Task EnqueueAsync(
        string operationName,
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return EnqueueAsync<bool>(
            operationName,
            async token =>
            {
                await operation(token);
                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private KeeneticUnavailableException TimeoutError(string operationName, string stage)
    {
        _logger.LogWarning(
            "NDMS: «{Operation}» превысила таймаут {TimeoutSeconds} с {Stage}",
            operationName,
            _timeout.TotalSeconds,
            stage);

        return new KeeneticUnavailableException(
            $"Операция «{operationName}» не уложилась в {_timeout.TotalSeconds:0} с ({stage})");
    }
}
