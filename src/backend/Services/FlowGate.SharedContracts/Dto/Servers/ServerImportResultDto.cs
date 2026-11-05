namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Итог пакетного импорта
/// </summary>
/// <param name="Added">Добавленные серверы</param>
/// <param name="Skipped">Пропущено как уже существующие</param>
/// <param name="Rejected">Отклонённые узлы</param>
/// <param name="Warnings">Предупреждения</param>
public sealed record ServerImportResultDto(
    IReadOnlyList<ServerDto> Added,
    int Skipped,
    IReadOnlyList<ServerImportRejectionDto> Rejected,
    IReadOnlyList<string> Warnings);
