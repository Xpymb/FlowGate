namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Узел, который не удалось импортировать
/// </summary>
/// <param name="Index">Номер узла во входных данных, с единицы</param>
/// <param name="Reason">Причина</param>
public sealed record ServerImportRejectionDto(int Index, string Reason);
