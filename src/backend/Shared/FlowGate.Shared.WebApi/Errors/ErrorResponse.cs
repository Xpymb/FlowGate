namespace FlowGate.Shared.WebApi.Errors;

/// <summary>
///     Тело ответа API при ошибке
/// </summary>
/// <param name="ErrorCode">Код ошибки из <see cref="FlowGate.Exceptions.ErrorCodes"/></param>
/// <param name="Message">Сообщение для пользователя</param>
/// <param name="TraceId">Идентификатор запроса для поиска в логах</param>
public sealed record ErrorResponse(string ErrorCode, string Message, string TraceId);
