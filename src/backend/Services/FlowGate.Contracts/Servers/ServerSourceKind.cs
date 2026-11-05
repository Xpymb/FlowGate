namespace FlowGate.Contracts.Servers;

/// <summary>
///     Откуда взялся сервер
/// </summary>
public enum ServerSourceKind
{
    /// <summary>
    ///     Добавлен пользователем вручную
    /// </summary>
    Manual,

    /// <summary>
    ///     Получен из подписки; правится только обновлением подписки
    /// </summary>
    Subscription,
}
