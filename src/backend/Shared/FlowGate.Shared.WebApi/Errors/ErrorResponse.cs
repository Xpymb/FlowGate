namespace FlowGate.Shared.WebApi.Errors;

/// <summary>
///     Тело ответа API при ошибке
/// </summary>
/// <param name="ErrorCode">Код ошибки из <see cref="FlowGate.Exceptions.ErrorCodes"/></param>
/// <param name="Message">Сообщение для пользователя</param>
/// <param name="TraceId">Идентификатор запроса для поиска в логах</param>
/// <param name="Details">Подробности, например отдельные ошибки проверки, или null</param>
public sealed record ErrorResponse(string ErrorCode, string Message, string TraceId, IReadOnlyList<string>? Details = null);
