namespace FlowGate.WebApi.Hosting;

/// <summary>
///     Режим запуска бинарника
/// </summary>
public enum FlowGateCommandKind
{
    /// <summary>
    ///     Веб-сервер (по умолчанию)
    /// </summary>
    Serve,

    /// <summary>
    ///     <c>flowgate cleanup</c>: удалить объекты FlowGate из Keenetic и выйти
    /// </summary>
    Cleanup,

    /// <summary>
    ///     <c>flowgate help</c>: показать справку
    /// </summary>
    Help,

    /// <summary>
    ///     Неизвестная команда
    /// </summary>
    Invalid,
}
