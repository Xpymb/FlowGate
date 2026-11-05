namespace FlowGate.Contracts.Connections;

/// <summary>
///     Итог сверки интерфейсов соединений
/// </summary>
/// <param name="Repaired">Сколько интерфейсов создано заново или исправлено</param>
/// <param name="RecreatedInterfaces">Интерфейсы, созданные заново: Keenetic удалил вместе с ними и DNS-правила</param>
public sealed record ConnectionReconcileResult(int Repaired, IReadOnlyList<string> RecreatedInterfaces)
{
    /// <summary>
    ///     Ничего не менялось
    /// </summary>
    public static ConnectionReconcileResult None { get; } = new(0, []);
}
