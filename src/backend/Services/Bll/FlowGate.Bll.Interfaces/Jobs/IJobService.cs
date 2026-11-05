using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.Bll.Interfaces.Jobs;

/// <summary>
///     Фоновые задачи для API
/// </summary>
public interface IJobService
{
    /// <summary>
    ///     Возвращает задачу
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <returns>Задача</returns>
    /// <exception cref="EntityNotFoundException">Если задачи нет или она вытеснена из истории</exception>
    JobDto Get(Guid id);

    /// <summary>
    ///     Возвращает выполняющиеся и последние завершённые задачи, новые первыми
    /// </summary>
    /// <returns>Задачи</returns>
    IReadOnlyList<JobDto> List();
}
