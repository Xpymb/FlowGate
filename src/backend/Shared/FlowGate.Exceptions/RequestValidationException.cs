namespace FlowGate.Exceptions;

/// <summary>
///     Запрос не прошёл валидацию (HTTP 400)
/// </summary>
public sealed class RequestValidationException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="RequestValidationException"/>
    /// </summary>
    /// <param name="message">Описание ошибки для клиента</param>
    public RequestValidationException(string message)
        : base(ErrorCodes.ValidationError, 400, message)
    {
    }
}
