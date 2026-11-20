namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Состояние прокси обновлений
/// </summary>
public enum UpdateProxyState
{
    /// <summary>
    ///     Выключен в настройках: обновления идут напрямую
    /// </summary>
    Disabled,

    /// <summary>
    ///     Включён, inbound работает в основном Xray
    /// </summary>
    Ready,

    /// <summary>
    ///     Включён, но недоступен (Xray не запущен, сервер не найден): обновления идут напрямую, причина — в
    ///     <see cref="UpdateProxyStatus.Reason"/>
    /// </summary>
    Unavailable,
}
