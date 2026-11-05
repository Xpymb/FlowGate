using System.ComponentModel;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Dal.Interfaces.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Генерация <c>xray.json</c>: модель → JSON → проверка <c>xray run -test</c> → атомарная замена
/// </summary>
public sealed class XrayConfigService : IXrayConfigService
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);
    private const int OutputTailLength = 600;

    private readonly IXrayConfigSource _source;
    private readonly IXrayConfigBuilder _builder;
    private readonly IXrayConfigFileProvider _configFile;
    private readonly ISettingsProvider _settings;
    private readonly IProcessRunner _processRunner;
    private readonly IXrayProcessService _process;
    private readonly ILogger<XrayConfigService> _logger;
    private readonly string _binaryPath;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayConfigService"/>
    /// </summary>
    /// <param name="source">Источник модели</param>
    /// <param name="builder">Сборщик JSON</param>
    /// <param name="configFile">Файл конфигурации</param>
    /// <param name="settings">Пользовательские настройки (уровень логирования)</param>
    /// <param name="processRunner">Запуск <c>xray run -test</c></param>
    /// <param name="process">Процесс Xray</param>
    /// <param name="options">Настройки Xray</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayConfigService(
        IXrayConfigSource source,
        IXrayConfigBuilder builder,
        IXrayConfigFileProvider configFile,
        ISettingsProvider settings,
        IProcessRunner processRunner,
        IXrayProcessService process,
        IOptions<XrayOptions> options,
        ILogger<XrayConfigService> logger)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
        _configFile = configFile ?? throw new ArgumentNullException(nameof(configFile));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
    }

    /// <inheritdoc />
    public async Task<bool> WriteAsync(string? testBinaryPath, CancellationToken cancellationToken)
    {
        var settings = await _settings.GetAsync(cancellationToken);
        var model = await _source.GetModelAsync(settings.XrayLogLevel, cancellationToken);
        var content = Serialize(_builder.Build(model));

        var current = await _configFile.ReadAsync(cancellationToken);
        if (current is not null && current.AsSpan().SequenceEqual(content))
        {
            if (testBinaryPath is not null)
            {
                await TestAsync(testBinaryPath, _configFile.ConfigPath, cancellationToken);
            }

            return false;
        }

        var candidate = await _configFile.WriteCandidateAsync(content, cancellationToken);
        try
        {
            if (testBinaryPath is not null)
            {
                await TestAsync(testBinaryPath, candidate, cancellationToken);
            }

            _configFile.Commit(candidate);
        }
        finally
        {
            _configFile.Discard(candidate);
        }

        _logger.LogInformation("Конфигурация Xray обновлена: {Inbounds} inbound, {Outbounds} outbound", model.Inbounds.Count, model.Outbounds.Count);
        return true;
    }

    /// <inheritdoc />
    public Task<bool> ApplyAsync(CancellationToken cancellationToken) =>
        _process.RunExclusiveAsync(
            async (control, token) =>
            {
                var changed = await WriteAsync(File.Exists(_binaryPath) ? _binaryPath : null, token);
                if (changed && control.IsRunning)
                {
                    await control.StopAsync(token);
                    await control.StartAsync(token);
                }

                return changed;
            },
            cancellationToken);

    private async Task TestAsync(string binaryPath, string configPath, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(binaryPath, ["run", "-test", "-c", configPath], TestTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            throw new XrayOperationException($"Не удалось проверить конфигурацию Xray: {ex.Message}", ex);
        }

        if (!result.Succeeded)
        {
            var output = result.CombinedOutput;
            var tail = output.Length > OutputTailLength ? output[^OutputTailLength..] : output;
            throw new XrayOperationException($"Xray отклонил конфигурацию: {tail}");
        }
    }

    private static byte[] Serialize(System.Text.Json.Nodes.JsonObject config)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            config.WriteTo(writer);
        }

        return buffer.ToArray();
    }
}
