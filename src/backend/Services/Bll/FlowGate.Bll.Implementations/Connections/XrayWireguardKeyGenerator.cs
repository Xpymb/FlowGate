using System.ComponentModel;

using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Connections;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Connections;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Ключи WireGuard из установленного Xray: <c>xray wg</c> печатает <c>PrivateKey:</c> и <c>Password (PublicKey):</c>
/// </summary>
public sealed class XrayWireguardKeyGenerator : IWireguardKeyGenerator
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly IProcessRunner _processRunner;
    private readonly IXrayVersionProvider _versionProvider;
    private readonly string _binaryPath;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayWireguardKeyGenerator"/>
    /// </summary>
    /// <param name="processRunner">Запуск команд</param>
    /// <param name="versionProvider">Сведения об установленном Xray</param>
    /// <param name="options">Настройки Xray (путь к бинарнику)</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayWireguardKeyGenerator(IProcessRunner processRunner, IXrayVersionProvider versionProvider, IOptions<XrayOptions> options)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        ArgumentNullException.ThrowIfNull(options);
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
    }

    /// <inheritdoc />
    public async Task<WireguardKeyPair> GenerateAsync(CancellationToken cancellationToken)
    {
        if (!_versionProvider.IsInstalled)
        {
            throw new RequestValidationException("Для WireGuard-соединения сначала установите Xray: ключи создаёт он");
        }

        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(_binaryPath, ["wg"], Timeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            throw new XrayOperationException($"Не удалось создать ключи WireGuard: {ex.Message}", ex);
        }

        if (!result.Succeeded)
        {
            throw new XrayOperationException($"xray wg завершился с кодом {result.ExitCode}");
        }

        return Parse(result.StandardOutput);
    }

    /// <summary>
    ///     Разбирает вывод <c>xray wg</c>
    /// </summary>
    /// <param name="output">Вывод команды</param>
    /// <returns>Пара ключей</returns>
    /// <exception cref="XrayOperationException">Если в выводе нет обоих ключей</exception>
    public static WireguardKeyPair Parse(string output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var values = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(':', 2, StringSplitOptions.TrimEntries))
            .Where(parts => parts.Length == 2 && parts[1].Length > 0)
            .ToList();

        var privateKey = values.FirstOrDefault(parts => parts[0] == "PrivateKey")?[1];
        var publicKey = values.FirstOrDefault(parts => parts[0].Contains("PublicKey", StringComparison.Ordinal))?[1];

        return privateKey is null || publicKey is null
            ? throw new XrayOperationException("xray wg вернул вывод без ключей")
            : new WireguardKeyPair(privateKey, publicKey);
    }
}
