using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки источников списков и применения DNS-маршрутов (секция <c>DomainSources</c>)
/// </summary>
public sealed class DomainSourceOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "DomainSources";

    /// <summary>
    ///     Как часто обновлять используемые источники с автообновлением, часы
    /// </summary>
    [Range(1, 720)]
    public int RefreshIntervalHours { get; set; } = 24;

    /// <summary>
    ///     Как часто планировщик проверяет, каким источникам пора обновиться, минуты
    /// </summary>
    [Range(1, 1440)]
    public int SchedulerIntervalMinutes { get; set; } = 30;

    /// <summary>
    ///     Предельный размер загружаемого файла источника, байты
    /// </summary>
    [Range(1024, 512 * 1024 * 1024)]
    public int MaxDownloadBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>
    ///     Предельный размер распакованных списков репозитория, байты
    /// </summary>
    [Range(1024, 256 * 1024 * 1024)]
    public int MaxRepositoryBytes { get; set; } = 20 * 1024 * 1024;

    /// <summary>
    ///     Таймаут загрузки по прямой ссылке, секунды
    /// </summary>
    [Range(1, 3600)]
    public int DownloadTimeoutSeconds { get; set; } = 300;

    /// <summary>
    ///     Предельное число элементов одного маршрута (во всех частях группы)
    /// </summary>
    [Range(1, 100000)]
    public int MaxEntriesPerRoute { get; set; } = 10000;

    /// <summary>
    ///     Команд в одном запросе RCI при применении маршрутов (~100 команд/с на роутере)
    /// </summary>
    [Range(1, 5000)]
    public int RciBatchSize { get; set; } = 500;
}
