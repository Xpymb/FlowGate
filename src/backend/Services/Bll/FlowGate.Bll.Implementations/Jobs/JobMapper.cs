using FlowGate.Shared.Bll.Jobs;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.Bll.Implementations.Jobs;

/// <summary>
///     Маппинг <see cref="JobSnapshot"/> в <see cref="JobDto"/>
/// </summary>
public static class JobMapper
{
    /// <summary>
    ///     Преобразует снимок задачи в DTO
    /// </summary>
    /// <param name="job">Снимок задачи</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если job равен null</exception>
    public static JobDto ToDto(this JobSnapshot job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return new JobDto(
            job.Id,
            job.Kind,
            job.Title,
            job.Status.ToString(),
            job.Steps.Select(step => new JobStepDto(step.At, step.Message)).ToList(),
            job.Error,
            job.StartedAt,
            job.FinishedAt);
    }
}
