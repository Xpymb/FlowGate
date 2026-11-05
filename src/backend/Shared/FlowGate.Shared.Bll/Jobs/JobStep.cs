namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Шаг фоновой задачи для показа прогресса
/// </summary>
/// <param name="At">Время шага</param>
/// <param name="Message">Описание шага</param>
public sealed record JobStep(DateTimeOffset At, string Message);
