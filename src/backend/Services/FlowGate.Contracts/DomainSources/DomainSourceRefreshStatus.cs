namespace FlowGate.Contracts.DomainSources;

/// <summary>
///     Результат обновления источника
/// </summary>
public enum DomainSourceRefreshStatus
{
    /// <summary>
    ///     Обновлён без ошибок или не изменился
    /// </summary>
    Ok,

    /// <summary>
    ///     Обновлён, но часть списков не разобрана
    /// </summary>
    Partial,

    /// <summary>
    ///     Обновить не удалось; прежний файл остался
    /// </summary>
    Failed,
}
