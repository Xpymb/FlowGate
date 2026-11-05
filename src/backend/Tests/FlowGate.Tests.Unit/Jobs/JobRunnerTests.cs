using Microsoft.Extensions.Logging.Abstractions;

using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;

namespace FlowGate.Tests.Unit.Jobs;

/// <summary>
///     Тесты <see cref="JobRunner"/>
/// </summary>
public sealed class JobRunnerTests : IDisposable
{
    private readonly JobRunner _sut = new(TimeProvider.System, NullLogger<JobRunner>.Instance);

    /// <summary>
    ///     Успешная задача сохраняет шаги и статус
    /// </summary>
    [Fact]
    public async Task Start_SuccessfulWork_CompletesWithSteps()
    {
        var started = _sut.Start("kind", "Задача", (context, _) =>
        {
            context.Report("шаг 1");
            context.Report("шаг 2");
            return Task.CompletedTask;
        });

        var finished = await _sut.WaitAsync(started.Id, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Running, started.Status);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
        Assert.Equal(["шаг 1", "шаг 2"], finished.Steps.Select(step => step.Message));
        Assert.NotNull(finished.FinishedAt);
    }

    /// <summary>
    ///     Исключение превращает задачу в Failed с сообщением
    /// </summary>
    [Fact]
    public async Task Start_FailingWork_CompletesAsFailedWithError()
    {
        var started = _sut.Start("kind", "Задача", (_, _) => throw new InvalidOperationException("сломалось"));

        var finished = await _sut.WaitAsync(started.Id, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Failed, finished!.Status);
        Assert.Equal("сломалось", finished.Error);
    }

    /// <summary>
    ///     Вторая задача того же вида во время первой отклоняется, другого вида — нет
    /// </summary>
    [Fact]
    public async Task Start_SameKindWhileRunning_ThrowsConflict()
    {
        var release = new TaskCompletionSource();
        var first = _sut.Start("install", "Первая", (_, _) => release.Task);

        Assert.Throws<ConflictException>(() => _sut.Start("install", "Вторая", (_, _) => Task.CompletedTask));
        var other = _sut.Start("refresh", "Другая", (_, _) => Task.CompletedTask);

        release.SetResult();
        await _sut.WaitAsync(first.Id, TestContext.Current.CancellationToken);
        await _sut.WaitAsync(other.Id, TestContext.Current.CancellationToken);
        var next = _sut.Start("install", "Третья", (_, _) => Task.CompletedTask);
        Assert.Equal(JobStatus.Succeeded, (await _sut.WaitAsync(next.Id, TestContext.Current.CancellationToken))!.Status);
    }

    /// <summary>
    ///     В истории остаются только последние завершённые задачи
    /// </summary>
    [Fact]
    public async Task List_ManyFinishedJobs_KeepsLatestHistory()
    {
        var ids = new List<Guid>();
        for (var index = 0; index < JobRunner.HistorySize + 5; index++)
        {
            var job = _sut.Start("kind", $"Задача {index}", (_, _) => Task.CompletedTask);
            await _sut.WaitAsync(job.Id, TestContext.Current.CancellationToken);
            ids.Add(job.Id);
        }

        var list = _sut.List();

        Assert.Equal(JobRunner.HistorySize, list.Count);
        Assert.Equal(ids[^1], list[0].Id);
        Assert.Null(_sut.Get(ids[0]));
    }

    /// <inheritdoc />
    public void Dispose() => _sut.Dispose();
}
