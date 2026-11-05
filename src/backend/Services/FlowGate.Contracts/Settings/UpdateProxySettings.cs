namespace FlowGate.Contracts.Settings;

/// <summary>
///     Загрузка обновлений (подписки, источники списков, релизы Xray, пакет FlowGate) через отдельный экземпляр Xray
///     с выбранным сервером
/// </summary>
public sealed record UpdateProxySettings
{
    /// <summary>
    ///     Прокси выключен
    /// </summary>
    public static UpdateProxySettings Default { get; } = new();

    private readonly IReadOnlyList<string>? _extraDomains;

    /// <summary>
    ///     Загружать обновления через прокси
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Идентификатор сервера, чей outbound используется
    /// </summary>
    public string? ServerId { get; init; }

    /// <summary>
    ///     Дополнительные домены в формате правил Xray (<c>domain:</c>, <c>full:</c> или хост)
    /// </summary>
    public IReadOnlyList<string> ExtraDomains
    {
        get => _extraDomains ?? [];
        init => _extraDomains = value;
    }

    /// <summary>
    ///     Сравнивает настройки по значению, включая список доменов
    /// </summary>
    /// <param name="other">Другие настройки</param>
    /// <returns>true, если настройки совпадают</returns>
    public bool SameAs(UpdateProxySettings other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Enabled == other.Enabled
            && string.Equals(ServerId, other.ServerId, StringComparison.Ordinal)
            && ExtraDomains.SequenceEqual(other.ExtraDomains, StringComparer.Ordinal);
    }
}
