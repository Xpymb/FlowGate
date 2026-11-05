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
    ///     Включён, экземпляр не запущен; запустится при первом запросе
    /// </summary>
    Idle,

    /// <summary>
    ///     Экземпляр работает
    /// </summary>
    Running,

    /// <summary>
    ///     Включён, но запустить не удалось: обновления идут напрямую, причина — в <see cref="UpdateProxyStatus.Reason"/>
    /// </summary>
    Unavailable,
}
