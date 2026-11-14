namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Серверы, найденные в тексте или подписке, без сохранения
/// </summary>
/// <param name="Title">Название профиля от провайдера подписки или null</param>
/// <param name="Servers">Принятые серверы</param>
/// <param name="Rejected">Отклонённые узлы</param>
/// <param name="Warnings">Предупреждения</param>
public sealed record ServerPreviewDto(
    string? Title,
    IReadOnlyList<ServerPreviewItemDto> Servers,
    IReadOnlyList<ServerImportRejectionDto> Rejected,
    IReadOnlyList<string> Warnings);
