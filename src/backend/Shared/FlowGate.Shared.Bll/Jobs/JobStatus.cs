namespace FlowGate.Shared.Bll.Jobs;

/// <summary>
///     Состояние фоновой задачи
/// </summary>
public enum JobStatus
{
    /// <summary>
    ///     Выполняется
    /// </summary>
    Running,

    /// <summary>
    ///     Завершилась успешно
    /// </summary>
    Succeeded,

    /// <summary>
    ///     Завершилась ошибкой
    /// </summary>
    Failed,
}
