namespace FlowGate.Exceptions;

/// <summary>
///     Базовое исключение FlowGate, которое превращается в ответ API с кодом ошибки и HTTP-статусом
/// </summary>
public abstract class FlowGateException : Exception
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FlowGateException"/>
    /// </summary>
    /// <param name="errorCode">Код ошибки из <see cref="ErrorCodes"/></param>
    /// <param name="statusCode">HTTP-статус ответа</param>
    /// <param name="message">Сообщение для клиента</param>
    /// <param name="innerException">Исходное исключение</param>
    protected FlowGateException(string errorCode, int statusCode, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }

    /// <summary>
    ///     Код ошибки из <see cref="ErrorCodes"/>
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    ///     HTTP-статус ответа
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    ///     Подробности для клиента (например список ошибок проверки) или null
    /// </summary>
    public IReadOnlyList<string>? Details { get; protected init; }
}
