using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки доступа к GitHub (секция <c>GitHub</c>)
/// </summary>
public sealed class GitHubOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "GitHub";

    /// <summary>
    ///     Базовый адрес API; можно заменить на зеркало
    /// </summary>
    [Required]
    public string ApiBaseUrl { get; set; } = "https://api.github.com/";

    /// <summary>
    ///     Таймаут запроса к API, секунды
    /// </summary>
    [Range(1, 600)]
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    ///     Таймаут скачивания одного файла, секунды
    /// </summary>
    [Range(1, 3600)]
    public int DownloadTimeoutSeconds { get; set; } = 600;
}
