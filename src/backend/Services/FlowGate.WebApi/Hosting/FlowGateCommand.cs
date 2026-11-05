namespace FlowGate.WebApi.Hosting;

/// <summary>
///     Разобранная командная строка: режим и аргументы для хоста (переопределения конфигурации вида
///     <c>--FlowGate:DataDirectory=/path</c>, <c>--urls</c>)
/// </summary>
/// <param name="Kind">Режим</param>
/// <param name="DryRun">Для <c>cleanup</c>: только показать команды</param>
/// <param name="HostArguments">Аргументы для построителя хоста</param>
/// <param name="Error">Сообщение для неизвестной команды</param>
public sealed record FlowGateCommand(FlowGateCommandKind Kind, bool DryRun, string[] HostArguments, string? Error)
{
    private const string DryRunOption = "--dry-run";

    /// <summary>
    ///     Справка по командной строке
    /// </summary>
    public const string Usage = """
        Использование:
          flowgate [--urls <адрес>] [--Секция:Ключ=значение ...]   запустить FlowGate
          flowgate cleanup [--dry-run] [--Секция:Ключ=значение ...] удалить объекты FlowGate из конфигурации Keenetic
          flowgate help                                            эта справка
        """;

    /// <summary>
    ///     Разбирает аргументы командной строки. Команда — первый аргумент, если он не начинается с <c>-</c>
    /// </summary>
    /// <param name="args">Аргументы процесса</param>
    /// <returns>Разобранная команда</returns>
    /// <exception cref="ArgumentNullException">Если args равен null</exception>
    public static FlowGateCommand Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0 || args[0].StartsWith('-'))
        {
            return new FlowGateCommand(FlowGateCommandKind.Serve, DryRun: false, args, Error: null);
        }

        var rest = args[1..];
        return args[0] switch
        {
            "cleanup" => new FlowGateCommand(
                FlowGateCommandKind.Cleanup,
                rest.Contains(DryRunOption, StringComparer.Ordinal),
                rest.Where(arg => !string.Equals(arg, DryRunOption, StringComparison.Ordinal)).ToArray(),
                Error: null),
            "help" or "--help" => new FlowGateCommand(FlowGateCommandKind.Help, DryRun: false, rest, Error: null),
            var unknown => new FlowGateCommand(FlowGateCommandKind.Invalid, DryRun: false, rest, $"Неизвестная команда «{unknown}»"),
        };
    }
}
