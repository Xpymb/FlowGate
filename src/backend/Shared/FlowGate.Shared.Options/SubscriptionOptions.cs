using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки загрузки и обновления подписок (секция <c>Subscriptions</c>)
/// </summary>
public sealed class SubscriptionOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "Subscriptions";

    /// <summary>
    ///     Таймаут загрузки подписки, секунды
    /// </summary>
    [Range(1, 600)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Предельный размер ответа после распаковки, байты
    /// </summary>
    [Range(1024, 64 * 1024 * 1024)]
    public int MaxResponseBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>
    ///     Предельное число серверов одной подписки
    /// </summary>
    [Range(1, 5000)]
    public int MaxServersPerSubscription { get; set; } = 500;

    /// <summary>
    ///     Интервал обновления, если его не задали ни пользователь, ни провайдер, часы
    /// </summary>
    [Range(1, 168)]
    public int DefaultUpdateIntervalHours { get; set; } = 24;

    /// <summary>
    ///     Через сколько минут повторить обновление после ошибки
    /// </summary>
    [Range(1, 1440)]
    public int RetryAfterFailureMinutes { get; set; } = 30;

    /// <summary>
    ///     Как часто планировщик проверяет, каким подпискам пора обновиться, минуты
    /// </summary>
    [Range(1, 1440)]
    public int SchedulerIntervalMinutes { get; set; } = 5;
}
