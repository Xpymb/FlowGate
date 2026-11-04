namespace FlowGate.Contracts.Servers;

/// <summary>
///     Узел, который не удалось разобрать
/// </summary>
/// <param name="Index">Порядковый номер узла во входных данных, с единицы</param>
/// <param name="Reason">Причина; без паролей и ключей</param>
public sealed record ServerImportRejection(int Index, string Reason);
