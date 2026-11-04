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

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="RequestValidationException"/> со списком ошибок
    /// </summary>
    /// <param name="message">Общее описание ошибки</param>
    /// <param name="details">Отдельные ошибки проверки</param>
    public RequestValidationException(string message, IReadOnlyList<string> details)
        : base(ErrorCodes.ValidationError, 400, message)
    {
        Details = details;
    }
}
