using System.Globalization;

namespace FlowGate.Contracts.Keenetic;

/// <summary>
///     Сведения о роутере Keenetic и правила его совместимости с FlowGate
/// </summary>
public sealed class KeeneticSystemInfo
{
    /// <summary>
    ///     Минимальная мажорная версия KeeneticOS: DNS-маршруты по object-group fqdn есть с 5-й версии
    /// </summary>
    public const int MinimumMajorVersion = 5;

    /// <summary>
    ///     Поддерживаемая архитектура
    /// </summary>
    public const string SupportedArchitecture = "aarch64";

    /// <summary>
    ///     Компоненты KeeneticOS, без которых FlowGate не работает: прокси-клиент, WireGuard и OPKG (Entware)
    /// </summary>
    public static IReadOnlyList<string> RequiredComponents { get; } = ["proxy", "wireguard", "opkg"];

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticSystemInfo"/>
    /// </summary>
    /// <param name="model">Модель</param>
    /// <param name="hardwareId">Индекс модели, например <c>KN-3812</c></param>
    /// <param name="release">Версия прошивки, например <c>5.02.B.2.0-2</c></param>
    /// <param name="title">Название версии</param>
    /// <param name="architecture">Архитектура</param>
    /// <param name="components">Установленные компоненты KeeneticOS</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public KeeneticSystemInfo(
        string model,
        string hardwareId,
        string release,
        string title,
        string architecture,
        IReadOnlyCollection<string> components)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        HardwareId = hardwareId ?? throw new ArgumentNullException(nameof(hardwareId));
        Release = release ?? throw new ArgumentNullException(nameof(release));
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Architecture = architecture ?? throw new ArgumentNullException(nameof(architecture));
        Components = components ?? throw new ArgumentNullException(nameof(components));
        MajorVersion = ParseMajorVersion(release);
    }

    /// <summary>
    ///     Модель
    /// </summary>
    public string Model { get; }

    /// <summary>
    ///     Индекс модели, например <c>KN-3812</c>
    /// </summary>
    public string HardwareId { get; }

    /// <summary>
    ///     Версия прошивки
    /// </summary>
    public string Release { get; }

    /// <summary>
    ///     Название версии
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Архитектура
    /// </summary>
    public string Architecture { get; }

    /// <summary>
    ///     Установленные компоненты KeeneticOS
    /// </summary>
    public IReadOnlyCollection<string> Components { get; }

    /// <summary>
    ///     Мажорная версия KeeneticOS или null, если версию не удалось разобрать
    /// </summary>
    public int? MajorVersion { get; }

    /// <summary>
    ///     Версия прошивки подходит
    /// </summary>
    public bool IsVersionSupported => MajorVersion >= MinimumMajorVersion;

    /// <summary>
    ///     Архитектура подходит
    /// </summary>
    public bool IsArchitectureSupported =>
        string.Equals(Architecture, SupportedArchitecture, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Обязательные компоненты, которые не установлены
    /// </summary>
    public IReadOnlyList<string> MissingComponents =>
        RequiredComponents
            .Where(required => !Components.Contains(required, StringComparer.OrdinalIgnoreCase))
            .ToList();

    /// <summary>
    ///     Роутер подходит для FlowGate
    /// </summary>
    public bool IsSupported => IsVersionSupported && IsArchitectureSupported && MissingComponents.Count == 0;

    /// <summary>
    ///     Причины несовместимости для показа пользователю; пусто, если роутер подходит
    /// </summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            var problems = new List<string>();

            if (!IsVersionSupported)
            {
                problems.Add($"Нужна KeeneticOS {MinimumMajorVersion} или новее, установлена {Release}");
            }

            if (!IsArchitectureSupported)
            {
                problems.Add($"Поддерживается только {SupportedArchitecture}, у роутера {Architecture}");
            }

            problems.AddRange(MissingComponents.Select(component => $"Не установлен компонент KeeneticOS «{component}»"));

            return problems;
        }
    }

    private static int? ParseMajorVersion(string release)
    {
        var majorPart = release.Split('.', 2)[0];
        return int.TryParse(majorPart, NumberStyles.None, CultureInfo.InvariantCulture, out var major) ? major : null;
    }
}
