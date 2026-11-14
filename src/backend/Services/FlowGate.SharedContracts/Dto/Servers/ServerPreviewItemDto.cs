namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Сервер в предпросмотре
/// </summary>
/// <param name="ImportKey">Ключ идентичности: тот же ключ получит сервер после сохранения (<see cref="ServerDto.ImportKey"/>)</param>
/// <param name="Name">Имя</param>
/// <param name="Protocol">Протокол Xray</param>
/// <param name="Address">Адрес</param>
/// <param name="Port">Порт или null</param>
/// <param name="Network">Транспорт</param>
/// <param name="Security">Защита</param>
public sealed record ServerPreviewItemDto(
    string ImportKey,
    string Name,
    string Protocol,
    string Address,
    int? Port,
    string Network,
    string Security);
