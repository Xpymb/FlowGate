namespace FlowGate.Contracts.Servers;

/// <summary>
///     Все серверы (содержимое <c>servers.json</c>)
/// </summary>
/// <param name="Servers">Серверы</param>
public sealed record ServerCatalog(IReadOnlyList<Server> Servers)
{
    /// <summary>
    ///     Пустой каталог
    /// </summary>
    public static ServerCatalog Empty { get; } = new([]);
}
