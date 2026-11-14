using Microsoft.Extensions.Logging;

using FlowGate.Exceptions;
using FlowGate.Shared.Changes;

namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Фоновые задачи в памяти с историей последних завершённых
/// </summary>
public sealed class JobRunner : IJobRunner, IDisposable
{
    /// <summary>
    ///     Сколько завершённых задач хранится в истории
    /// </summary>
    public const int HistorySize = 20;

    private readonly Lock _sync = new();
    private readonly List<JobState> _jobs = [];
    private readonly CancellationTokenSource _shutdown = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<JobRunner> _logger;
    private readonly IChangeFeed? _changes;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="JobRunner"/>
    /// </summary>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <param name="changes">Шина изменений: тема <see cref="ChangeTopics.Jobs"/> на запуск, шаг и завершение; null — не сообщать</param>
    /// <exception cref="ArgumentNullException">Если timeProvider или logger равны null</exception>
    public JobRunner(TimeProvider timeProvider, ILogger<JobRunner> logger, IChangeFeed? changes = null)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _changes = changes;
    }

    /// <inheritdoc />
    public JobSnapshot Start(string kind, string title, Func<IJobContext, CancellationToken, Task> work)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(work);

        JobState job;
        lock (_sync)
        {
            if (_jobs.FirstOrDefault(existing => existing.Kind == kind && existing.Status == JobStatus.Running) is { } running)
            {
                throw new ConflictException($"Уже выполняется: {running.Title}");
            }

            job = new JobState(Guid.NewGuid(), kind, title, _timeProvider.GetUtcNow(), _sync, _timeProvider, NotifyChanged);
            _jobs.Insert(0, job);
        }

        _logger.LogInformation("Задача {JobId} «{Title}» запущена", job.Id, title);
        NotifyChanged();
        job.Completion = Task.Run(() => RunAsync(job, work));
        return job.ToSnapshot();
    }

    /// <inheritdoc />
    public JobSnapshot? Get(Guid id)
    {
        lock (_sync)
        {
            return _jobs.FirstOrDefault(job => job.Id == id)?.ToSnapshot();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<JobSnapshot> List()
    {
        lock (_sync)
        {
            return _jobs.Select(job => job.ToSnapshot()).ToList();
        }
    }

    /// <inheritdoc />
    public async Task<JobSnapshot?> WaitAsync(Guid id, CancellationToken cancellationToken)
    {
        Task? completion;
        lock (_sync)
        {
            completion = _jobs.FirstOrDefault(job => job.Id == id)?.Completion;
        }

        if (completion is null)
        {
            return Get(id);
        }

        await completion.WaitAsync(cancellationToken);
        return Get(id);
    }

    /// <inheritdoc />
    public void Dispose() => _shutdown.Cancel();

    private async Task RunAsync(JobState job, Func<IJobContext, CancellationToken, Task> work)
    {
        try
        {
            await work(job, _shutdown.Token);
            job.Finish(JobStatus.Succeeded, null);
            _logger.LogInformation("Задача {JobId} «{Title}» выполнена", job.Id, job.Title);
        }
        catch (Exception ex)
        {
            job.Finish(JobStatus.Failed, ex.Message);
            _logger.LogWarning(ex, "Задача {JobId} «{Title}» завершилась ошибкой: {Error}", job.Id, job.Title, ex.Message);
        }
        finally
        {
            TrimHistory();
            NotifyChanged();
        }
    }

    private void NotifyChanged() => _changes?.Publish(ChangeTopics.Jobs);

    private void TrimHistory()
    {
        lock (_sync)
        {
            var finished = _jobs.Where(job => job.Status != JobStatus.Running).Skip(HistorySize).ToList();
            foreach (var job in finished)
            {
                _jobs.Remove(job);
            }
        }
    }

    private sealed class JobState : IJobContext
    {
        private readonly Lock _sync;
        private readonly TimeProvider _timeProvider;
        private readonly List<JobStep> _steps = [];
        private readonly Action _onReport;

        public JobState(Guid id, string kind, string title, DateTimeOffset startedAt, Lock sync, TimeProvider timeProvider, Action onReport)
        {
            _onReport = onReport;
            Id = id;
            Kind = kind;
            Title = title;
            StartedAt = startedAt;
            _sync = sync;
            _timeProvider = timeProvider;
        }

        public Guid Id { get; }

        public string Kind { get; }

        public string Title { get; }

        public DateTimeOffset StartedAt { get; }

        public JobStatus Status { get; private set; } = JobStatus.Running;

        public string? Error { get; private set; }

        public DateTimeOffset? FinishedAt { get; private set; }

        public Task? Completion { get; set; }

        Guid IJobContext.JobId => Id;

        public void Report(string message)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(message);

            lock (_sync)
            {
                _steps.Add(new JobStep(_timeProvider.GetUtcNow(), message));
            }

            _onReport();
        }

        public void Finish(JobStatus status, string? error)
        {
            lock (_sync)
            {
                Status = status;
                Error = error;
                FinishedAt = _timeProvider.GetUtcNow();
            }
        }

        public JobSnapshot ToSnapshot()
        {
            lock (_sync)
            {
                return new JobSnapshot(Id, Kind, Title, Status, _steps.ToList(), Error, StartedAt, FinishedAt);
            }
        }
    }
}
