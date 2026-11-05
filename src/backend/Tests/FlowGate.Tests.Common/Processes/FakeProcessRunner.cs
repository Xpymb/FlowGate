using FlowGate.Shared.Bll.Processes;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Common.Processes;

/// <summary>
///     Подменный запуск команд. По умолчанию ведёт себя как Xray: <c>version</c> печатает версию из
///     <see cref="FakeXrayBinary"/>, <c>wg</c> печатает новую пару ключей, <c>run -test</c> успешен
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

        if (arguments is ["wg"])
        {
            return new ProcessResult(0, WireguardKeysOutput(), string.Empty);
        }

        return new ProcessResult(0, "Configuration OK.", string.Empty);
    }

    /// <summary>
    ///     Вывод <c>xray wg</c> со случайными ключами в формате Xray 26
    /// </summary>
    /// <returns>Вывод команды</returns>
    public static string WireguardKeysOutput()
    {
        var privateKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        var publicKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        return string.Join(
            '\n',
            $"PrivateKey: {privateKey}",
            $"Password (PublicKey): {publicKey}",
            $"Hash32: {Convert.ToBase64String(new byte[32])}");
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
