namespace FlowGate.Contracts.Xray;

/// <summary>
///     Удалось ли узнать выбор балансировщиков Xray
/// </summary>
public enum XrayBalancerSupport
{
    /// <summary>
    ///     Балансировщиков нет, спрашивать нечего
    /// </summary>
    NotNeeded,

    /// <summary>
    ///     Выбор получен
    /// </summary>
    Supported,

    /// <summary>
    ///     Эта версия Xray не даёт API балансировщика: отклонила блок <c>api</c>, не знает команду или отвечает в
    ///     неизвестном формате
    /// </summary>
    Unsupported,

    /// <summary>
    ///     Сейчас узнать нельзя: Xray не установлен или не запущен, API не ответил
    /// </summary>
    Unavailable,
}
