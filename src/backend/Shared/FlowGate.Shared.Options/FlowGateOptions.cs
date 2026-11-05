using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Общие настройки приложения (секция <c>FlowGate</c>)
/// </summary>
public sealed class FlowGateOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "FlowGate";

    /// <summary>
    ///     Каталог данных приложения (JSON-файлы настроек, серверов, маршрутов). На роутере — <c>/opt/etc/flowgate</c>
    /// </summary>
    [Required]
    public string DataDirectory { get; set; } = "/opt/etc/flowgate";

    /// <summary>
    ///     Каталог со статикой фронта. Если каталога нет, бэкенд работает только как API
    /// </summary>
    [Required]
    public string WebRootPath { get; set; } = "/opt/share/flowgate/www";
}
