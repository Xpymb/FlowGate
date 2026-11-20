namespace FlowGate.Contracts.Settings;

/// <summary>
///     Загрузка обновлений (подписки, источники списков, релизы Xray, пакет FlowGate) через inbound основного Xray
///     с выбранным сервером
/// </summary>
public sealed record UpdateProxySettings
{
    /// <summary>
    ///     Прокси выключен
    /// </summary>
    public static UpdateProxySettings Default { get; } = new();

    /// <summary>
    ///     Загружать обновления через прокси
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    ///     Идентификатор сервера, чей outbound используется. Пока он задан, inbound прокси есть в конфиге Xray
    ///     независимо от <see cref="Enabled"/>
    /// </summary>
    public string? ServerId { get; init; }
}
