using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.UpdateProxy;

/// <summary>
///     Прокси обновлений — inbound <see cref="UpdateProxyXrayModel.InboundTag"/> в основном Xray. Сервис только решает,
///     можно ли им пользоваться: выбран существующий сервер, основной Xray запущен и принимает соединения на порту.
///     Сам Xray не запускает
/// </summary>
public sealed class UpdateProxyService : IUpdateProxyService
{
    /// <summary>
    ///     Имя файла конфигурации экземпляра для скрипта самообновления в <see cref="XrayOptions.WorkDirectory"/>
    /// </summary>
    public const string StandaloneConfigFileName = "update-proxy-upgrade.json";

    private const string LogLevel = "warning";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    private readonly Lock _sync = new();
    private readonly ISettingsProvider _settings;
    private readonly IServerProvider _servers;
    private readonly IXrayProcessService _xray;
    private readonly IXrayConfigBuilder _builder;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger<UpdateProxyService> _logger;
    private readonly UpdateProxyOptions _options;
    private readonly string _binaryPath;
    private readonly string _workDirectory;
    private readonly Uri _proxyUri;

    private string? _reason;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyService"/>
    /// </summary>
    /// <param name="settings">Пользовательские настройки (включён ли прокси, сервер)</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="xray">Процесс основного Xray</param>
    /// <param name="builder">Сборщик конфигурации Xray (экземпляр самообновления)</param>
    /// <param name="processRunner">Запуск <c>xray run -test</c></param>
    /// <param name="xrayOptions">Настройки Xray: бинарник и рабочий каталог</param>
    /// <param name="options">Настройки прокси обновлений</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public UpdateProxyService(
        ISettingsProvider settings,
        IServerProvider servers,
        IXrayProcessService xray,
        IXrayConfigBuilder builder,
        IProcessRunner processRunner,
        IOptions<XrayOptions> xrayOptions,
        IOptions<UpdateProxyOptions> options,
        ILogger<UpdateProxyService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _xray = xray ?? throw new ArgumentNullException(nameof(xray));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentNullException.ThrowIfNull(xrayOptions);
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _binaryPath = Path.GetFullPath(xrayOptions.Value.BinaryPath);
        _workDirectory = Path.GetFullPath(xrayOptions.Value.WorkDirectory);
        _proxyUri = new Uri($"http://{UpdateProxyXrayModel.ListenAddress}:{_options.Port}/");
    }

    /// <inheritdoc />
    public async Task<Uri?> ResolveAsync(UpdateVia via, CancellationToken cancellationToken)
    {
        if (via == UpdateVia.Direct)
        {
            return null;
        }

        var settings = await _settings.GetAsync(cancellationToken);
        if (via == UpdateVia.Auto && !settings.UpdateProxy.Enabled)
        {
            return null;
        }

        var reason = await FindProblemAsync(settings.UpdateProxy, cancellationToken);
        if (reason is null)
        {
            lock (_sync)
            {
                _reason = null;
            }

            return _proxyUri;
        }

        ReportUnavailable(reason);
        if (via == UpdateVia.Proxy)
        {
            throw new ExternalServiceUnavailableException($"Прокси обновлений недоступен: {reason}");
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<UpdateProxyStatus> GetStatusAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.UpdateProxy.Enabled)
        {
            return new UpdateProxyStatus(UpdateProxyState.Disabled, _options.Port, null);
        }

        var problem = await FindProblemAsync(settings.UpdateProxy, cancellationToken);
        return problem is null
            ? new UpdateProxyStatus(UpdateProxyState.Ready, _options.Port, null)
            : new UpdateProxyStatus(UpdateProxyState.Unavailable, _options.Port, problem);
    }

    /// <inheritdoc />
    public async Task<UpdateProxyStandaloneConfig?> PrepareStandaloneAsync(CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        if (!settings.UpdateProxy.Enabled)
        {
            return null;
        }

        var path = Path.Combine(_workDirectory, StandaloneConfigFileName);
        try
        {
            var outbound = await GetOutboundAsync(settings.UpdateProxy, cancellationToken);
            if (!File.Exists(_binaryPath))
            {
                throw new UnavailableException("Xray не установлен");
            }

            var config = Build(UpdateProxyXrayModel.Build(LogLevel, outbound, _options.UpgradePort));
            await WriteTestedAsync(config, path, cancellationToken);
            return new UpdateProxyStandaloneConfig(_binaryPath, path, _options.UpgradePort);
        }
        catch (UnavailableException ex)
        {
            ReportUnavailable(ex.Message);
            return null;
        }
    }

    private async Task<string?> FindProblemAsync(UpdateProxySettings settings, CancellationToken cancellationToken)
    {
        try
        {
            await GetOutboundAsync(settings, cancellationToken);
        }
        catch (UnavailableException ex)
        {
            return ex.Message;
        }

        return _xray.GetStatus().State switch
        {
            XrayProcessState.Running => await CanConnectAsync(cancellationToken) ? null : $"Xray не принимает соединения на порту {_options.Port}",
            XrayProcessState.NotInstalled => "Xray не установлен",
            _ => "Xray не запущен",
        };
    }

    private async Task<JsonObject> GetOutboundAsync(UpdateProxySettings settings, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(settings.ServerId))
        {
            throw new UnavailableException("сервер не выбран");
        }

        var server = await _servers.GetAsync(settings.ServerId, cancellationToken)
            ?? throw new UnavailableException($"сервер {settings.ServerId} не найден");

        return server.Outbound;
    }

    private byte[] Build(XrayConfigModel model)
    {
        try
        {
            return Serialize(_builder.Build(model));
        }
        catch (XrayOperationException ex)
        {
            throw new UnavailableException(ex.Message);
        }
    }

    /// <remarks>Xray определяет формат конфига по расширению, поэтому кандидат тоже оканчивается на <c>.json</c></remarks>
    private async Task WriteTestedAsync(byte[] config, string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var candidate = Path.ChangeExtension(path, ".new.json");
        try
        {
            await File.WriteAllBytesAsync(candidate, config, cancellationToken);
            await TestAsync(candidate, cancellationToken);
            File.Move(candidate, path, overwrite: true);
        }
        finally
        {
            File.Delete(candidate);
        }
    }

    private async Task TestAsync(string configPath, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(_binaryPath, ["run", "-test", "-c", configPath], TestTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            throw new UnavailableException($"не удалось проверить конфигурацию: {ex.Message}");
        }

        if (!result.Succeeded)
        {
            var errors = OutboundValidator.ExtractErrors(result.CombinedOutput, configPath);
            throw new UnavailableException($"Xray отклонил конфигурацию: {string.Join("; ", errors)}");
        }
    }

    private async Task<bool> CanConnectAsync(CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(ProbeTimeout);

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(IPAddress.Loopback, _options.Port, timeout.Token);
            return true;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private void ReportUnavailable(string reason)
    {
        bool changed;
        lock (_sync)
        {
            changed = !string.Equals(_reason, reason, StringComparison.Ordinal);
            _reason = reason;
        }

        if (changed)
        {
            _logger.LogWarning("Прокси обновлений недоступен: {Reason}", reason);
        }
    }

    private static byte[] Serialize(JsonObject config)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            config.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    private sealed class UnavailableException(string message) : Exception(message);
}
