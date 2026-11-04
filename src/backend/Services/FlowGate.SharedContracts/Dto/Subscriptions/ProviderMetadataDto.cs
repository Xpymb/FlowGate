namespace FlowGate.SharedContracts.Dto.Subscriptions;

/// <summary>
///     Сведения от провайдера. Тексты и ссылки — данные провайдера, показывать как текст
/// </summary>
/// <param name="Title">Название профиля</param>
/// <param name="UploadBytes">Отправлено, байты</param>
/// <param name="DownloadBytes">Получено, байты</param>
/// <param name="TotalBytes">Лимит трафика, байты; null — не сообщён</param>
/// <param name="ExpireAt">Срок действия; null — не сообщён</param>
/// <param name="UpdateIntervalHours">Рекомендуемый интервал обновления, часы</param>
/// <param name="Announce">Объявление</param>
/// <param name="AnnounceUrl">Ссылка объявления</param>
/// <param name="SupportUrl">Поддержка</param>
/// <param name="WebPageUrl">Страница профиля</param>
public sealed record ProviderMetadataDto(
    string? Title,
    long? UploadBytes,
    long? DownloadBytes,
    long? TotalBytes,
    DateTimeOffset? ExpireAt,
    int? UpdateIntervalHours,
    string? Announce,
    string? AnnounceUrl,
    string? SupportUrl,
    string? WebPageUrl);
