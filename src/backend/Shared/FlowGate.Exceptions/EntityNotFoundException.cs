namespace FlowGate.Exceptions;

/// <summary>
///     Сущность не найдена (HTTP 404)
/// </summary>
public sealed class EntityNotFoundException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="EntityNotFoundException"/>
    /// </summary>
    /// <param name="entityName">Название сущности</param>
    /// <param name="id">Идентификатор, по которому искали</param>
    public EntityNotFoundException(string entityName, object id)
        : base(ErrorCodes.EntityNotFound, 404, $"{entityName} «{id}» не найден(а)")
    {
    }
}
