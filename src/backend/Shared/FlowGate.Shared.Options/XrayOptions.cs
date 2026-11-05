using System.ComponentModel.DataAnnotations;

namespace FlowGate.Shared.Options;

/// <summary>
///     Настройки управления Xray (секция <c>Xray</c>)
/// </summary>
public sealed class XrayOptions
{
    /// <summary>
    ///     Имя секции конфигурации
    /// </summary>
    public const string SectionName = "Xray";

    /// <summary>
    ///     Путь к бинарнику Xray. Рядом хранятся <c>.new</c> и <c>.bak</c> на время обновления
    /// </summary>
    [Required]
    public string BinaryPath { get; set; } = "/opt/sbin/flowgate-xray";

    /// <summary>
    ///     Имя файла конфигурации Xray в каталоге данных
    /// </summary>
    [Required]
    public string ConfigFileName { get; set; } = "xray.json";

    /// <summary>
    ///     Рабочий каталог для скачивания релизов и pid-файла. На роутере — tmpfs
    /// </summary>
    [Required]
    public string WorkDirectory { get; set; } = "/tmp/flowgate";

    /// <summary>
    ///     Репозиторий релизов в формате <c>owner/repo</c>
    /// </summary>
    [Required]
    public string Repository { get; set; } = "XTLS/Xray-core";

    /// <summary>
    ///     Имя архива для aarch64 в релизе; рядом лежит одноимённый <c>.dgst</c>
    /// </summary>
    [Required]
    public string ReleaseAsset { get; set; } = "Xray-linux-arm64-v8a.zip";

    /// <summary>
    ///     Сколько последних релизов показывать
    /// </summary>
    [Range(1, 100)]
    public int ReleasesCount { get; set; } = 10;

    /// <summary>
    ///     Сколько секунд процесс должен проработать после запуска, чтобы запуск считался успешным
    /// </summary>
    [Range(0, 60)]
    public int StartTimeoutSeconds { get; set; } = 3;

    /// <summary>
    ///     Сколько последних строк вывода Xray хранить для UI
    /// </summary>
    [Range(10, 10000)]
    public int LogBufferLines { get; set; } = 500;

    /// <summary>
    ///     Минимум свободного места (МиБ) в рабочем каталоге и рядом с бинарником перед установкой
    /// </summary>
    [Range(0, 10000)]
    public int MinFreeSpaceMegabytes { get; set; } = 80;

    /// <summary>
    ///     Интервал проверки новых релизов, часы
    /// </summary>
    [Range(1, 720)]
    public int UpdateCheckIntervalHours { get; set; } = 24;
}
