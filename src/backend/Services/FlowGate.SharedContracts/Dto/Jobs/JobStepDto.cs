namespace FlowGate.SharedContracts.Dto.Jobs;

/// <summary>
///     Шаг фоновой задачи
/// </summary>
/// <param name="At">Время шага</param>
/// <param name="Message">Описание шага</param>
public sealed record JobStepDto(DateTimeOffset At, string Message);
