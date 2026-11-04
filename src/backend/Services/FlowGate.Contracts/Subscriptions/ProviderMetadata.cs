namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Сведения, которые провайдер сообщает о подписке (заголовки ответа или строки <c>#key: value</c> в теле)
/// </summary>
/// <param name="Title">Название профиля (<c>profile-title</c>)</param>
/// <param name="UploadBytes">Отправлено, байты (<c>subscription-userinfo</c>)</param>
/// <param name="DownloadBytes">Получено, байты</param>
/// <param name="TotalBytes">Лимит трафика, байты; null или 0 — провайдер не сообщил</param>
/// <param name="ExpireAt">Срок действия</param>
/// <param name="UpdateIntervalHours">Рекомендуемый интервал обновления (<c>profile-update-interval</c>), 1–168 ч</param>
/// <param name="Announce">Объявление провайдера (<c>announce</c>), показывается как текст</param>
/// <param name="AnnounceUrl">Ссылка к объявлению</param>
/// <param name="SupportUrl">Поддержка (<c>support-url</c>)</param>
/// <param name="WebPageUrl">Страница профиля (<c>profile-web-page-url</c>)</param>
public sealed record ProviderMetadata(
    string? Title,
    long? UploadBytes,
    long? DownloadBytes,
    long? TotalBytes,
    DateTimeOffset? ExpireAt,
    int? UpdateIntervalHours,
    string? Announce,
    string? AnnounceUrl,
    string? SupportUrl,
    string? WebPageUrl)
{
    /// <summary>
    ///     Провайдер ничего не сообщил
    /// </summary>
    public bool IsEmpty =>
        Title is null && UploadBytes is null && DownloadBytes is null && TotalBytes is null && ExpireAt is null
        && UpdateIntervalHours is null && Announce is null && AnnounceUrl is null && SupportUrl is null && WebPageUrl is null;
}
