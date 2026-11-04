namespace FlowGate.Exceptions;

/// <summary>
///     RCI Keenetic недоступен, не ответил вовремя или ответил в неожиданном формате (HTTP 503)
/// </summary>
public sealed class KeeneticUnavailableException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticUnavailableException"/>
    /// </summary>
    /// <param name="message">Описание проблемы</param>
    /// <param name="innerException">Исходное исключение</param>
    public KeeneticUnavailableException(string message, Exception? innerException = null)
        : base(ErrorCodes.KeeneticUnavailable, 503, message, innerException)
    {
    }
}
