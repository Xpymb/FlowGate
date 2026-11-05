namespace FlowGate.Contracts.Connections;

/// <summary>
///     Все соединения (содержимое <c>connections.json</c>)
/// </summary>
/// <param name="Connections">Соединения</param>
public sealed record ConnectionCatalog(IReadOnlyList<Connection> Connections)
{
    /// <summary>
    ///     Пустой каталог
    /// </summary>
    public static ConnectionCatalog Empty { get; } = new([]);
}
