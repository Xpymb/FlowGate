namespace FlowGate.SharedContracts.Dto.Servers;

/// <summary>
///     Сервер в списке
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя</param>
/// <param name="Protocol">Протокол Xray</param>
/// <param name="Address">Адрес</param>
/// <param name="Port">Порт или null</param>
/// <param name="Network">Транспорт</param>
/// <param name="Security">Защита</param>
/// <param name="ServerName">SNI</param>
/// <param name="Source"><c>Manual</c> или <c>Subscription</c></param>
/// <param name="SubscriptionId">Подписка-источник или null</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
/// <param name="ImportKey">Ключ идентичности без учётных данных: совпадает с ключом в предпросмотре (<see cref="ServerPreviewItemDto.ImportKey"/>)</param>
public sealed record ServerDto(
    string Id,
    string Name,
    string Protocol,
    string Address,
    int? Port,
    string Network,
    string Security,
    string ServerName,
    string Source,
    string? SubscriptionId,
    DateTimeOffset UpdatedAt,
    string ImportKey);
