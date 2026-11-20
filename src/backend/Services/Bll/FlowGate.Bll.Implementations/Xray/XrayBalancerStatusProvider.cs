using System.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Выбор балансировщиков через <c>xray api bi -json</c>, запущенный мимо менеджера процессов, как <c>-test</c>.
///     «Не поддерживается» — API отклонён конфигом, команда или формат неизвестны, порт API не слушается при
///     работающем Xray; «недоступно» — Xray не работает, таймаут, конфиг как раз меняется
/// </summary>
public sealed class XrayBalancerStatusProvider : IXrayBalancerStatusProvider, IDisposable
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

    private readonly IXrayApiState _apiState;
    private readonly IXrayProcessService _process;
    private readonly IXrayVersionProvider _versionProvider;
    private readonly IProcessRunner _processRunner;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<XrayBalancerStatusProvider> _logger;
    private readonly string _binaryPath;
    private readonly string _apiAddress;
    private readonly SemaphoreSlim _sync = new(1, 1);
    private (string Key, DateTimeOffset Until, XrayBalancerStatus Status)? _cache;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayBalancerStatusProvider"/>
    /// </summary>
    /// <param name="apiState">Есть ли API в действующем конфиге</param>
    /// <param name="process">Состояние основного Xray</param>
    /// <param name="versionProvider">Установлен ли Xray</param>
    /// <param name="processRunner">Запуск <c>xray api bi</c></param>
    /// <param name="options">Настройки Xray: бинарник и порт API</param>
    /// <param name="timeProvider">Источник времени для кеша</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayBalancerStatusProvider(
        IXrayApiState apiState,
        IXrayProcessService process,
        IXrayVersionProvider versionProvider,
        IProcessRunner processRunner,
        IOptions<XrayOptions> options,
        TimeProvider timeProvider,
        ILogger<XrayBalancerStatusProvider> logger)
    {
        _apiState = apiState ?? throw new ArgumentNullException(nameof(apiState));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
        _apiAddress = $"127.0.0.1:{options.Value.ApiPort}";
    }

    /// <inheritdoc />
    public async Task<XrayBalancerStatus> GetAsync(IReadOnlyList<string> balancerTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(balancerTags);

        if (balancerTags.Count == 0)
        {
            return XrayBalancerStatus.Without(XrayBalancerSupport.NotNeeded);
        }

        if (_apiState.Availability == XrayApiAvailability.Rejected)
        {
            return XrayBalancerStatus.Without(XrayBalancerSupport.Unsupported);
        }

        if (!_versionProvider.IsInstalled || _process.GetStatus().State != XrayProcessState.Running)
        {
            return XrayBalancerStatus.Without(XrayBalancerSupport.Unavailable);
        }

        var key = string.Join(',', balancerTags);
        await _sync.WaitAsync(cancellationToken);
        try
        {
            if (_cache is { } cached && cached.Key == key && _timeProvider.GetUtcNow() < cached.Until)
            {
                return cached.Status;
            }

            var status = await QueryAsync(balancerTags, cancellationToken);
            _cache = (key, _timeProvider.GetUtcNow() + CacheDuration, status);
            return status;
        }
        finally
        {
            _sync.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _sync.Dispose();

    private async Task<XrayBalancerStatus> QueryAsync(IReadOnlyList<string> balancerTags, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(
                _binaryPath,
                ["api", "bi", "-s", _apiAddress, "-json", .. balancerTags],
                CommandTimeout,
                cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            _logger.LogDebug("Выбор балансировщиков Xray не получен: {Error}", ex.Message);
            return XrayBalancerStatus.Without(XrayBalancerSupport.Unavailable);
        }

        if (result.Succeeded)
        {
            if (XrayBalancerInfoParser.TryParse(result.StandardOutput, balancerTags, out var active))
            {
                return new XrayBalancerStatus(XrayBalancerSupport.Supported, active);
            }

            _logger.LogWarning("Ответ xray api bi в неизвестном формате: выбор балансировщиков этой версией Xray не поддерживается");
            return XrayBalancerStatus.Without(XrayBalancerSupport.Unsupported);
        }

        var output = result.CombinedOutput;
        if (output.Contains("cannot find tag", StringComparison.OrdinalIgnoreCase))
        {
            return XrayBalancerStatus.Without(XrayBalancerSupport.Unavailable);
        }

        _logger.LogDebug("xray api bi завершился с кодом {ExitCode}: {Output}", result.ExitCode, output.Length > 200 ? output[..200] : output);
        return XrayBalancerStatus.Without(XrayBalancerSupport.Unsupported);
    }
}
