using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки соединений: порты inbound Xray и адреса WireGuard-интерфейсов (секция <c>Connections</c>)
/// </summary>
public sealed class ConnectionOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "Connections";

    /// <summary>
    ///     Адрес, на котором слушают inbound Xray; Keenetic подключается к нему со своих интерфейсов
    /// </summary>
    [Required]
    public string ListenAddress { get; set; } = "127.0.0.1";

    /// <summary>
    ///     Первый порт для inbound соединений
    /// </summary>
    [Range(1024, 65535)]
    public int PortRangeStart { get; set; } = 10800;

    /// <summary>
    ///     Последний порт для inbound соединений
    /// </summary>
    [Range(1024, 65535)]
    public int PortRangeEnd { get; set; } = 10899;

    /// <summary>
    ///     Первые три октета подсети /24 для адресов WireGuard-интерфейсов Keenetic, например <c>10.255.0</c>
    /// </summary>
    [Required]
    [RegularExpression(@"^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){2}$")]
    public string WireguardSubnetPrefix { get; set; } = "10.255.0";

    /// <summary>
    ///     MTU WireGuard-интерфейса и inbound
    /// </summary>
    [Range(1280, 1500)]
    public int WireguardMtu { get; set; } = 1420;

    /// <summary>
    ///     Интервал keepalive WireGuard-пира, секунды
    /// </summary>
    [Range(0, 3600)]
    public int WireguardKeepaliveSeconds { get; set; } = 25;

    /// <summary>
    ///     Как часто сверять интерфейсы соединений с Keenetic, минуты
    /// </summary>
    [Range(1, 1440)]
    public int ReconcileIntervalMinutes { get; set; } = 10;
}
