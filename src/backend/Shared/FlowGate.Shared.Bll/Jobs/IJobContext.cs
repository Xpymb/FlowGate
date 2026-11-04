namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Контекст выполняющейся фоновой задачи
/// </summary>
public interface IJobContext
{
    /// <summary>
    ///     Идентификатор задачи
    /// </summary>
    Guid JobId { get; }

    /// <summary>
    ///     Добавляет шаг прогресса, видимый пользователю
    /// </summary>
    /// <param name="message">Описание шага</param>
    void Report(string message);
}
