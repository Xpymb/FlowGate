namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Список нельзя собрать: его нет, вложенного списка нет, include образуют цикл или файл источника повреждён
/// </summary>
public sealed class DomainListException : Exception
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainListException"/>
    /// </summary>
    /// <param name="message">Описание для пользователя</param>
    public DomainListException(string message)
        : base(message)
    {
    }

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="DomainListException"/>
    /// </summary>
    /// <param name="message">Описание для пользователя</param>
    /// <param name="innerException">Исходное исключение</param>
    public DomainListException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
