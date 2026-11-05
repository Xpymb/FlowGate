namespace FlowGate.Exceptions;

/// <summary>
///     Операция конфликтует с уже выполняющейся (HTTP 409)
/// </summary>
public sealed class ConflictException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConflictException"/>
    /// </summary>
    /// <param name="message">Описание конфликта для клиента</param>
    public ConflictException(string message)
        : base(ErrorCodes.Conflict, 409, message)
    {
    }
}
