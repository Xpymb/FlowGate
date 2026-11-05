using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Установленная версия Xray по выводу <c>xray version</c>; результат кэшируется до <see cref="Invalidate"/>
/// </summary>
public sealed class XrayVersionProvider : IXrayVersionProvider
{
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(15);

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<XrayVersionProvider> _logger;
    private readonly string _binaryPath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private XrayVersion? _cached;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayVersionProvider"/>
    /// </summary>
    /// <param name="options">Настройки Xray</param>
    /// <param name="processRunner">Запуск команд</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayVersionProvider(IOptions<XrayOptions> options, IProcessRunner processRunner, ILogger<XrayVersionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
    }

    /// <inheritdoc />
    public bool IsInstalled => File.Exists(_binaryPath);

    /// <inheritdoc />
    public async Task<XrayVersion?> GetInstalledVersionAsync(CancellationToken cancellationToken)
    {
        if (!IsInstalled)
        {
            Invalidate();
            return null;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cached is not null)
            {
                return _cached;
            }

            var result = await _processRunner.RunAsync(_binaryPath, ["version"], VersionTimeout, cancellationToken);
            if (result.Succeeded && XrayVersion.TryParse(result.StandardOutput, out var version))
            {
                _cached = version;
                return version;
            }

            _logger.LogWarning("Не удалось определить версию Xray: код {ExitCode}, вывод {Output}", result.ExitCode, result.CombinedOutput);
            return null;
        }
        catch (Exception ex) when (ex is TimeoutException or System.ComponentModel.Win32Exception or IOException)
        {
            _logger.LogWarning(ex, "Не удалось запустить {Binary} version", _binaryPath);
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Invalidate() => _cached = null;
}
