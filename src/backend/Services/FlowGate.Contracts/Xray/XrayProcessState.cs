namespace FlowGate.Contracts.Xray;

/// <summary>
///     Состояние процесса Xray
/// </summary>
public enum XrayProcessState
{
    /// <summary>
    ///     Бинарник Xray не установлен
    /// </summary>
    NotInstalled,

    /// <summary>
    ///     Установлен, но не запущен
    /// </summary>
    Stopped,

    /// <summary>
    ///     Запускается
    /// </summary>
    Starting,

    /// <summary>
    ///     Работает
    /// </summary>
    Running,

    /// <summary>
    ///     Упал и ждёт перезапуска
    /// </summary>
    Restarting,

    /// <summary>
    ///     Не удалось запустить
    /// </summary>
    Failed,
}
