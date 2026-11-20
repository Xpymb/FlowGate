using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки пинга серверов: HTTP-запрос через outbound во временном экземпляре Xray (секция <c>Ping</c>)
/// </summary>
public sealed class PingOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "Ping";

    /// <summary>
    ///     Адрес, который запрашивается через сервер; ответ любого кода считается успехом
    /// </summary>
    [Required]
    public string Url { get; set; } = "https://www.gstatic.com/generate_204";

    /// <summary>
    ///     Таймаут запроса через один сервер, секунды
    /// </summary>
    [Range(1, 30)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Сколько серверов пингуется одновременно
    /// </summary>
    [Range(1, 32)]
    public int MaxConcurrency { get; set; } = 8;

    /// <summary>
    ///     Наибольшее число серверов в одном запросе пинга
    /// </summary>
    [Range(1, 100)]
    public int MaxServersPerRequest { get; set; } = 25;

    /// <summary>
    ///     Сколько ждать, пока временный Xray откроет порты, секунды
    /// </summary>
    [Range(1, 30)]
    public int StartTimeoutSeconds { get; set; } = 3;
}
