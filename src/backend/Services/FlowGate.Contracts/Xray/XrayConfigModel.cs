namespace FlowGate.Contracts.Xray;

/// <summary>
///     Модель конфигурации Xray, из которой собирается <c>xray.json</c>
/// </summary>
/// <param name="LogLevel">Уровень логирования: <c>debug</c>, <c>info</c>, <c>warning</c>, <c>error</c>, <c>none</c></param>
/// <param name="Inbounds">Точки входа</param>
/// <param name="Outbounds">Исходящие соединения, кроме встроенных <c>direct</c> и <c>block</c></param>
/// <param name="Rules">Правила маршрутизации</param>
public sealed record XrayConfigModel(
    string LogLevel,
    IReadOnlyList<XrayInbound> Inbounds,
    IReadOnlyList<XrayOutbound> Outbounds,
    IReadOnlyList<XrayRoutingRule> Rules)
{
    /// <summary>
    ///     Тег встроенного прямого outbound (freedom)
    /// </summary>
    public const string DirectTag = "direct";

    /// <summary>
    ///     Тег встроенного блокирующего outbound (blackhole)
    /// </summary>
    public const string BlockTag = "block";

    /// <summary>
    ///     Допустимые уровни логирования
    /// </summary>
    public static IReadOnlyList<string> LogLevels { get; } = ["debug", "info", "warning", "error", "none"];

    /// <summary>
    ///     Пустая модель: ни одного соединения
    /// </summary>
    /// <param name="logLevel">Уровень логирования</param>
    /// <returns>Модель без inbound, outbound и правил</returns>
    public static XrayConfigModel Empty(string logLevel) => new(logLevel, [], [], []);
}
