using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Прокси обновлений — отдельный экземпляр Xray, через который качаются подписки, источники и релизы
///     (секция <c>UpdateProxy</c>). Включается и выбирает сервер пользователь в настройках
/// </summary>
public sealed class UpdateProxyOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "UpdateProxy";

    /// <summary>
    ///     Порт HTTP-inbound на <c>127.0.0.1</c>; не должен попадать в диапазон портов соединений
    /// </summary>
    [Range(1024, 65535)]
    public int Port { get; set; } = 10790;

    /// <summary>
    ///     Порт экземпляра, который запускает скрипт самообновления: он живёт дольше службы FlowGate
    /// </summary>
    [Range(1024, 65535)]
    public int UpgradePort { get; set; } = 10791;

    /// <summary>
    ///     Сколько секунд ждать, пока экземпляр начнёт принимать соединения
    /// </summary>
    [Range(1, 120)]
    public int StartTimeoutSeconds { get; set; } = 10;
}
