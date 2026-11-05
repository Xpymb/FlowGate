namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Результат короткой внешней команды
/// </summary>
/// <param name="ExitCode">Код выхода</param>
/// <param name="StandardOutput">Стандартный вывод</param>
/// <param name="StandardError">Поток ошибок</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    ///     Команда завершилась с кодом 0
    /// </summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>
    ///     Вывод и ошибки одной строкой — для сообщений пользователю
    /// </summary>
    public string CombinedOutput => string.Join(
        Environment.NewLine,
        new[] { StandardOutput.Trim(), StandardError.Trim() }.Where(part => part.Length > 0));
}
