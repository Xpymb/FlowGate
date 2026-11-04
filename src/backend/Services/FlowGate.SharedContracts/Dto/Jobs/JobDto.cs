namespace FlowGate.SharedContracts.Dto.Jobs;

/// <summary>
///     Фоновая задача: долгая операция, за которой следит UI
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Kind">Вид задачи, например <c>xray-install</c></param>
/// <param name="Title">Название для пользователя</param>
/// <param name="Status">Состояние: <c>Running</c>, <c>Succeeded</c>, <c>Failed</c></param>
/// <param name="Steps">Пройденные шаги</param>
/// <param name="Error">Ошибка, если задача упала</param>
/// <param name="StartedAt">Время запуска</param>
/// <param name="FinishedAt">Время завершения или null</param>
public sealed record JobDto(
    Guid Id,
    string Kind,
    string Title,
    string Status,
    IReadOnlyList<JobStepDto> Steps,
    string? Error,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);
