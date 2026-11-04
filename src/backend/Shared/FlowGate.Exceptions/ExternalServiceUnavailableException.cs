namespace FlowGate.Exceptions;

/// <summary>
///     Внешний сервис (GitHub, сервер подписки) недоступен или ответил ошибкой (HTTP 503)
/// </summary>
public sealed class ExternalServiceUnavailableException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ExternalServiceUnavailableException"/>
    /// </summary>
    /// <param name="message">Описание проблемы</param>
    /// <param name="innerException">Исходное исключение</param>
    public ExternalServiceUnavailableException(string message, Exception? innerException = null)
        : base(ErrorCodes.ExternalUnavailable, 503, message, innerException)
    {
    }
}
