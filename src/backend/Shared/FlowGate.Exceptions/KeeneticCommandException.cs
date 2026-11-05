namespace FlowGate.Exceptions;

/// <summary>
///     Keenetic отклонил одну или несколько команд (HTTP 502)
/// </summary>
public sealed class KeeneticCommandException : FlowGateException
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticCommandException"/>
    /// </summary>
    /// <param name="failures">Описания отклонённых команд в виде «команда: код сообщение»</param>
    /// <exception cref="ArgumentNullException">Если failures равен null</exception>
    public KeeneticCommandException(IReadOnlyList<string> failures)
        : base(ErrorCodes.KeeneticCommandFailed, 502, BuildMessage(failures))
    {
        Failures = failures;
    }

    /// <summary>
    ///     Описания отклонённых команд
    /// </summary>
    public IReadOnlyList<string> Failures { get; }

    private static string BuildMessage(IReadOnlyList<string> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);

        return failures.Count switch
        {
            0 => "Keenetic отклонил команду",
            1 => $"Keenetic отклонил команду: {failures[0]}",
            _ => $"Keenetic отклонил {failures.Count} команд(ы): {string.Join("; ", failures)}",
        };
    }
}
