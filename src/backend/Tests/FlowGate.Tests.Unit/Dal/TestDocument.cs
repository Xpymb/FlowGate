namespace FlowGate.Tests.Unit.Dal;

/// <summary>
///     Документ для тестов хранилища JSON-файлов
/// </summary>
/// <param name="Counter">Счётчик</param>
/// <param name="Name">Имя</param>
public sealed record TestDocument(int Counter, string Name)
{
    /// <summary>
    ///     Значение по умолчанию
    /// </summary>
    public static TestDocument Default { get; } = new(0, "default");
}
