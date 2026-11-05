using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки самого FlowGate: проверка новых версий и самообновление через opkg (секция <c>System</c>)
/// </summary>
public sealed class SystemOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "System";

    /// <summary>
    ///     Репозиторий GitHub с релизами FlowGate (<c>owner/repo</c>)
    /// </summary>
    [Required]
    [RegularExpression(@"^[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")]
    public string Repository { get; set; } = "Xpymb/FlowGate";

    /// <summary>
    ///     Интервал проверки новых версий, часы
    /// </summary>
    [Range(1, 168)]
    public int UpdateCheckIntervalHours { get; set; } = 24;

    /// <summary>
    ///     Скрипт самообновления из пакета; его отсутствие значит, что FlowGate запущен не из пакета
    /// </summary>
    [Required]
    public string UpgradeScriptPath { get; set; } = "/opt/share/flowgate/upgrade.sh";

    /// <summary>
    ///     Журнал самообновления; последняя строка — итог (<c>ok …</c> / <c>failed …</c>)
    /// </summary>
    [Required]
    public string UpgradeLogPath { get; set; } = "/tmp/flowgate/upgrade.log";

    /// <summary>
    ///     <c>start-stop-daemon</c>: запускает скрипт обновления отвязанным от FlowGate процессом
    /// </summary>
    [Required]
    public string StartStopDaemonPath { get; set; } = "/opt/sbin/start-stop-daemon";
}
