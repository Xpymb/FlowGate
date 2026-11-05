namespace FlowGate.Contracts.Settings;

/// <summary>
///     Пользовательские настройки FlowGate (<c>settings.json</c>). Новые поля — init-свойства, которые вместо null отдают
///     значение по умолчанию: генератор JSON передаёт null для полей, которых нет в файле прежней версии
/// </summary>
/// <param name="XrayAutostart">Запускать Xray при старте FlowGate; кнопки «Старт»/«Стоп» в UI меняют это значение</param>
/// <param name="XrayLogLevel">Уровень логирования Xray</param>
public sealed record AppSettings(bool XrayAutostart, string XrayLogLevel)
{
    /// <summary>
    ///     Настройки по умолчанию
    /// </summary>
    public static AppSettings Default { get; } = new(XrayAutostart: true, XrayLogLevel: "warning");

    private readonly UpdateProxySettings? _updateProxy;
    private readonly UpdateIntervalSettings? _intervals;

    /// <summary>
    ///     Загрузка обновлений через прокси
    /// </summary>
    public UpdateProxySettings UpdateProxy
    {
        get => _updateProxy ?? UpdateProxySettings.Default;
        init => _updateProxy = value;
    }

    /// <summary>
    ///     Интервалы обновлений; незаданные берутся из конфигурации
    /// </summary>
    public UpdateIntervalSettings Intervals
    {
        get => _intervals ?? UpdateIntervalSettings.Default;
        init => _intervals = value;
    }
}
