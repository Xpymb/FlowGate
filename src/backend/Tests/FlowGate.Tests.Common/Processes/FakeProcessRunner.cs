using FlowGate.Shared.Bll.Processes;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Common.Processes;

/// <summary>
///     Подменный запуск команд. По умолчанию ведёт себя как Xray: <c>version</c> печатает версию из
///     <see cref="FakeXrayBinary"/>, <c>run -test</c> успешен
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly List<(string FileName, IReadOnlyList<string> Arguments)> _calls = [];
    private readonly Lock _sync = new();

    /// <summary>
    ///     Обработчик команды; можно заменить в тесте
    /// </summary>
    public Func<string, IReadOnlyList<string>, ProcessResult> Handler { get; set; } = RespondAsXray;

    /// <summary>
    ///     Выполненные команды
    /// </summary>
    public IReadOnlyList<(string FileName, IReadOnlyList<string> Arguments)> Calls
    {
        get
        {
            lock (_sync)
            {
                return _calls.ToList();
            }
        }
    }

    /// <summary>
    ///     Поведение Xray по умолчанию
    /// </summary>
    /// <param name="fileName">Исполняемый файл</param>
    /// <param name="arguments">Аргументы</param>
    /// <returns>Результат команды</returns>
    public static ProcessResult RespondAsXray(string fileName, IReadOnlyList<string> arguments)
    {
        if (arguments is ["version"])
        {
            var version = FakeXrayBinary.ReadVersion(fileName);
            return version is null
                ? new ProcessResult(1, string.Empty, "not an xray binary")
                : new ProcessResult(0, FakeXrayBinary.VersionOutput(version), string.Empty);
        }

        return new ProcessResult(0, "Configuration OK.", string.Empty);
    }

    /// <inheritdoc />
    public Task<ProcessResult> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        lock (_sync)
        {
            _calls.Add((fileName, arguments.ToList()));
        }

        return Task.FromResult(Handler(fileName, arguments));
    }
}
