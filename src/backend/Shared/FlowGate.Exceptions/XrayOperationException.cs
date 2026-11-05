namespace FlowGate.Exceptions;

/// <summary>
///     Не удалась операция с Xray: установка, запуск или применение конфигурации (HTTP 500)
/// </summary>
public sealed class XrayOperationException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayOperationException"/>
    /// </summary>
    /// <param name="message">Описание проблемы, при необходимости с выводом xray</param>
    /// <param name="innerException">Исходное исключение</param>
    public XrayOperationException(string message, Exception? innerException = null)
        : base(ErrorCodes.XrayOperationFailed, 500, message, innerException)
    {
    }
}
