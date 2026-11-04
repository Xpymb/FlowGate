namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Снимок состояния фоновой задачи
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Kind">Вид задачи, например <c>xray-install</c>; одновременно идёт не больше одной задачи вида</param>
/// <param name="Title">Название для пользователя</param>
/// <param name="Status">Состояние</param>
/// <param name="Steps">Пройденные шаги</param>
/// <param name="Error">Ошибка, если задача упала</param>
/// <param name="StartedAt">Время запуска</param>
/// <param name="FinishedAt">Время завершения или null</param>
public sealed record JobSnapshot(
    Guid Id,
    string Kind,
    string Title,
    JobStatus Status,
    IReadOnlyList<JobStep> Steps,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
