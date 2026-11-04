namespace FlowGate.Contracts.Servers;

/// <summary>
///     Ссылку на сервер не удалось разобрать. Сообщение не содержит паролей и ключей из ссылки
/// </summary>
public sealed class ServerLinkFormatException : Exception
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerLinkFormatException"/>
    /// </summary>
    /// <param name="message">Причина</param>
    public ServerLinkFormatException(string message)
        : base(message)
    {
    }
}
