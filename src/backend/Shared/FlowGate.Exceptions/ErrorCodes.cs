namespace FlowGate.Exceptions;

/// <summary>
///     Коды ошибок API, которые получает клиент в поле <c>errorCode</c>
/// </summary>
public static class ErrorCodes
{
    /// <summary>
    ///     Сущность не найдена
    /// </summary>
    public const string EntityNotFound = "ENTITY_NOT_FOUND";

    /// <summary>
    ///     Запрос не прошёл валидацию
    /// </summary>
    public const string ValidationError = "VALIDATION_ERROR";

    /// <summary>
    ///     Запрос не удалось разобрать (тело, параметры маршрута)
    /// </summary>
    public const string BadRequest = "BAD_REQUEST";

    /// <summary>
    ///     Keenetic отклонил одну или несколько команд
    /// </summary>
    public const string KeeneticCommandFailed = "KEENETIC_COMMAND_FAILED";

    /// <summary>
    ///     RCI Keenetic недоступен или ответил неожиданно
    /// </summary>
    public const string KeeneticUnavailable = "KEENETIC_UNAVAILABLE";

    /// <summary>
    ///     Непредвиденная ошибка сервера
    /// </summary>
    public const string InternalError = "INTERNAL_ERROR";
}
