namespace FlowGate.Contracts.Settings;

/// <summary>
///     Пользовательские настройки FlowGate (<c>settings.json</c>)
/// </summary>
/// <param name="XrayAutostart">Запускать Xray при старте FlowGate; кнопки «Старт»/«Стоп» в UI меняют это значение</param>
/// <param name="XrayLogLevel">Уровень логирования Xray</param>
public sealed record AppSettings(bool XrayAutostart, string XrayLogLevel)
{
    /// <summary>
    ///     Настройки по умолчанию
    /// </summary>
    public static AppSettings Default { get; } = new(XrayAutostart: true, XrayLogLevel: "warning");
}
