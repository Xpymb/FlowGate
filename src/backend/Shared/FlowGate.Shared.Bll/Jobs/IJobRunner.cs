using FlowGate.Exceptions;

namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Фоновые задачи в памяти: долгие операции, за прогрессом которых следит UI
/// </summary>
public interface IJobRunner
{
    /// <summary>
    ///     Запускает задачу в фоне и сразу возвращает её снимок
    /// </summary>
    /// <param name="kind">Вид задачи; одновременно выполняется не больше одной задачи вида</param>
    /// <param name="title">Название для пользователя</param>
    /// <param name="work">Работа; исключение превращает задачу в <see cref="JobStatus.Failed"/></param>
    /// <returns>Снимок только что запущенной задачи</returns>
    /// <exception cref="ConflictException">Если задача этого вида уже выполняется</exception>
    JobSnapshot Start(string kind, string title, Func<IJobContext, CancellationToken, Task> work);

    /// <summary>
    ///     Возвращает снимок задачи
    /// </summary>
    /// <param name="id">Идентификатор задачи</param>
    /// <returns>Снимок или null, если задачи нет (или она вытеснена из истории)</returns>
    JobSnapshot? Get(Guid id);

    /// <summary>
    ///     Возвращает задачи, новые первыми
    /// </summary>
    /// <returns>Выполняющиеся и последние завершённые задачи</returns>
    IReadOnlyList<JobSnapshot> List();

    /// <summary>
    ///     Ждёт завершения задачи
    /// </summary>
    /// <param name="id">Идентификатор задачи</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Снимок завершённой задачи или null, если задачи нет</returns>
    Task<JobSnapshot?> WaitAsync(Guid id, CancellationToken cancellationToken);
}
