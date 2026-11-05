using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки взаимодействия с Keenetic (секция <c>Keenetic</c>)
/// </summary>
public sealed class KeeneticOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "Keenetic";

    /// <summary>
    ///     Базовый адрес RCI. Локально на роутере доступен без авторизации
    /// </summary>
    [Required]
    public string RciBaseUrl { get; set; } = "http://127.0.0.1:79/rci/";

    /// <summary>
    ///     Таймаут одного HTTP-запроса к RCI, секунды. Пакет из 1000 команд выполняется около 10 с
    /// </summary>
    [Range(1, 600)]
    public int RequestTimeoutSeconds { get; set; } = 60;

    /// <summary>
    ///     Таймаут операции в очереди NDMS (ожидание очереди и выполнение), секунды
    /// </summary>
    [Range(1, 3600)]
    public int OperationTimeoutSeconds { get; set; } = 180;

    /// <summary>
    ///     Максимум доменов в одной группе <c>object-group fqdn</c>. На KeeneticOS 5.2 лимит — 1000
    /// </summary>
    [Range(1, 10000)]
    public int MaxDomainsPerGroup { get; set; } = 1000;
}
