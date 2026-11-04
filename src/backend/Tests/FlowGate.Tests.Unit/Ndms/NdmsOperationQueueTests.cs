using Microsoft.Extensions.Logging.Abstractions;

using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Options;

namespace FlowGate.Tests.Unit.Ndms;

/// <summary>
///     Тесты <see cref="NdmsOperationQueue"/>
/// </summary>
public sealed class NdmsOperationQueueTests : IDisposable
{
    private readonly NdmsOperationQueue _sut = CreateQueue(timeoutSeconds: 30);

    /// <summary>
    ///     Параллельные операции выполняются строго по одной
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_ConcurrentOperations_RunOneAtATime()
    {
        var running = 0;
        var maxRunning = 0;

        var operations = Enumerable.Range(0, 8).Select(index => _sut.EnqueueAsync(
            $"op-{index}",
            async token =>
            {
                var current = Interlocked.Increment(ref running);
                InterlockedMax(ref maxRunning, current);
                await Task.Delay(15, token);
                Interlocked.Decrement(ref running);
                return index;
            },
            TestContext.Current.CancellationToken));

        var results = await Task.WhenAll(operations);

        Assert.Equal(1, maxRunning);
        Assert.Equal(Enumerable.Range(0, 8), results.Order());
    }

    /// <summary>
    ///     Упавшая операция освобождает очередь для следующих
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_AfterFailedOperation_ReleasesQueue()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.EnqueueAsync<int>(
            "failing",
            _ => throw new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken));

        var result = await _sut.EnqueueAsync("next", _ => Task.FromResult(42), TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    /// <summary>
    ///     Операция дольше таймаута отменяется и превращается в KeeneticUnavailableException
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_OperationExceedsTimeout_ThrowsKeeneticUnavailable()
    {
        using var sut = CreateQueue(timeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<KeeneticUnavailableException>(() => sut.EnqueueAsync(
            "slow",
            token => Task.Delay(Timeout.Infinite, token),
            TestContext.Current.CancellationToken));

        Assert.Contains("slow", exception.Message);
    }

    /// <summary>
    ///     Отмена вызывающей стороной не маскируется под таймаут
    /// </summary>
    [Fact]
    public async Task EnqueueAsync_CallerCancels_ThrowsOperationCanceled()
    {
        using var cancellation = new CancellationTokenSource();
        var operation = _sut.EnqueueAsync(
            "cancelled",
            token => Task.Delay(Timeout.Infinite, token),
            cancellation.Token);

        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    /// <inheritdoc />
    public void Dispose() => _sut.Dispose();

    private static NdmsOperationQueue CreateQueue(int timeoutSeconds) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new KeeneticOptions { OperationTimeoutSeconds = timeoutSeconds }),
            NullLogger<NdmsOperationQueue>.Instance);

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        do
        {
            current = target;
            if (value <= current)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref target, value, current) != current);
    }
}
