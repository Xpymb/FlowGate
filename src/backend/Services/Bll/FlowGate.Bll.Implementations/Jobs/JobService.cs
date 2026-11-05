using FlowGate.Bll.Interfaces.Jobs;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.Bll.Implementations.Jobs;

/// <summary>
///     Фоновые задачи для API поверх <see cref="IJobRunner"/>
/// </summary>
public sealed class JobService : IJobService
{
    private readonly IJobRunner _jobs;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="JobService"/>
    /// </summary>
    /// <param name="jobs">Фоновые задачи</param>
    /// <exception cref="ArgumentNullException">Если jobs равен null</exception>
    public JobService(IJobRunner jobs)
    {
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
    }

    /// <inheritdoc />
    public JobDto Get(Guid id) =>
        (_jobs.Get(id) ?? throw new EntityNotFoundException("Задача", id)).ToDto();

    /// <inheritdoc />
    public IReadOnlyList<JobDto> List() => _jobs.List().Select(job => job.ToDto()).ToList();
}
