namespace FlowGate.Contracts.Servers;

/// <summary>
///     Результат разбора текста со ссылками или Xray-JSON
/// </summary>
/// <param name="Servers">Разобранные серверы</param>
/// <param name="Rejected">Отклонённые узлы</param>
/// <param name="Warnings">Предупреждения, например о проигнорированных разделах профиля</param>
/// <param name="BodyMetadata">Метаданные провайдера из тела (<c>#key: value</c>), ключи в нижнем регистре</param>
public sealed record ServerImportBatch(
    IReadOnlyList<ParsedServer> Servers,
    IReadOnlyList<ServerImportRejection> Rejected,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, string> BodyMetadata)
{
    /// <summary>
    ///     Сколько узлов получено: принятые и отклонённые
    /// </summary>
    public int Received => Servers.Count + Rejected.Count;
}
