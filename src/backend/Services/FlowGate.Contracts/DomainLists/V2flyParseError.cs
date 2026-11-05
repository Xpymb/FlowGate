namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Ошибка разбора строки списка
/// </summary>
/// <param name="Line">Номер строки, с 1</param>
/// <param name="Message">Описание</param>
public sealed record V2flyParseError(int Line, string Message)
{
    /// <inheritdoc />
    public override string ToString() => $"строка {Line}: {Message}";
}
