using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Implementations.Settings;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Управление Xray для API поверх процесса, релизов, установщика и конфигурации
/// </summary>
public sealed class XrayService : IXrayService
{
    private readonly IXrayProcessService _process;
    private readonly IXrayProcessManager _processes;
    private readonly IXrayVersionProvider _versionProvider;
    private readonly IXrayReleaseService _releases;
    private readonly IXrayInstaller _installer;
    private readonly IXrayConfigService _configService;
    private readonly ISettingsProvider _settings;
    private readonly IXrayLogBuffer _logs;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<XrayService> _logger;
    private readonly XrayOptions _options;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayService"/>
    /// </summary>
    /// <param name="process">Процесс Xray</param>
    /// <param name="processes">Менеджер процессов Xray (чужие процессы при старте)</param>
    /// <param name="versionProvider">Установленная версия</param>
    /// <param name="releases">Релизы</param>
    /// <param name="installer">Установщик</param>
    /// <param name="configService">Конфигурация</param>
    /// <param name="settings">Пользовательские настройки</param>
    /// <param name="logs">Вывод Xray</param>
    /// <param name="options">Настройки Xray</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayService(
        IXrayProcessService process,
        IXrayProcessManager processes,
        IXrayVersionProvider versionProvider,
        IXrayReleaseService releases,
        IXrayInstaller installer,
        IXrayConfigService configService,
        ISettingsProvider settings,
        IXrayLogBuffer logs,
        IOptions<XrayOptions> options,
        TimeProvider timeProvider,
        ILogger<XrayService> logger)
    {
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _processes = processes ?? throw new ArgumentNullException(nameof(processes));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _releases = releases ?? throw new ArgumentNullException(nameof(releases));
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logs = logs ?? throw new ArgumentNullException(nameof(logs));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<XrayStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var version = await _versionProvider.GetInstalledVersionAsync(cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        return _process.GetStatus().ToDto(_versionProvider.IsInstalled, version, settings.XrayAutostart, _releases.LastCheck);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XrayReleaseDto>> GetReleasesAsync(
        bool includePrerelease,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var releases = await _releases.GetReleasesAsync(refresh, cancellationToken);
        var installed = await _versionProvider.GetInstalledVersionAsync(cancellationToken);
        var latestStable = releases.FirstOrDefault(release => !release.IsPrerelease)?.Version;

        return releases
            .Where(release => includePrerelease || !release.IsPrerelease)
            .Select(release => release.ToDto(installed, latestStable))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<JobDto> InstallAsync(XrayInstallRequest request, UpdateVia via, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        XrayVersion? version = null;
        if (!string.IsNullOrWhiteSpace(request.Version) && !XrayVersion.TryParse(request.Version, out version))
        {
            throw new RequestValidationException($"Неверная версия Xray: «{request.Version}»");
        }

        var job = await _installer.StartInstallAsync(version, via, cancellationToken);
        return job.ToDto();
    }

    /// <inheritdoc />
    public async Task<XrayStatusDto> StartAsync(CancellationToken cancellationToken)
    {
        await _settings.UpdateAsync(settings => settings with { XrayAutostart = true }, cancellationToken);
        await _configService.WriteAsync(InstalledBinaryOrNull(), cancellationToken);
        await _process.StartAsync(cancellationToken);
        return await GetStatusAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<XrayStatusDto> StopAsync(CancellationToken cancellationToken)
    {
        await _settings.UpdateAsync(settings => settings with { XrayAutostart = false }, cancellationToken);
        await _process.StopAsync(cancellationToken);
        return await GetStatusAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<XrayStatusDto> RestartAsync(CancellationToken cancellationToken)
    {
        await _configService.WriteAsync(InstalledBinaryOrNull(), cancellationToken);
        await _process.RestartAsync(cancellationToken);
        return await GetStatusAsync(cancellationToken);
    }

    /// <inheritdoc />
    public XrayLogDto GetLogs(int tail)
    {
        var count = Math.Clamp(tail, 1, _options.LogBufferLines);
        return new XrayLogDto(_logs.Tail(count).Select(line => line.ToDto()).ToList());
    }

    /// <inheritdoc />
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await _processes.KillStrangersAsync(cancellationToken);

        if (!_versionProvider.IsInstalled)
        {
            _logger.LogInformation("Xray не установлен: установите его из интерфейса FlowGate");
            return;
        }

        try
        {
            await _configService.WriteAsync(InstalledBinaryOrNull(), cancellationToken);

            var settings = await _settings.GetAsync(cancellationToken);
            if (settings.XrayAutostart)
            {
                await _process.StartAsync(cancellationToken);
            }
        }
        catch (XrayOperationException ex)
        {
            _logger.LogError(ex, "Xray не запущен при старте FlowGate: {Error}", ex.Message);
        }
    }

    /// <inheritdoc />
    public Task ShutdownAsync(CancellationToken cancellationToken) => _process.StopAsync(cancellationToken);

    /// <inheritdoc />
    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        var check = await _releases.CheckForUpdatesAsync(cancellationToken);
        var installed = await _versionProvider.GetInstalledVersionAsync(cancellationToken);

        if (installed is not null && check.LatestVersion is { } latest && latest > installed)
        {
            _logger.LogInformation("Доступна новая версия Xray {Latest} (установлена {Installed})", latest, installed);
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckForUpdatesIfDueAsync(CancellationToken cancellationToken)
    {
        var interval = UpdateIntervals.XrayUpdateCheck(await _settings.GetAsync(cancellationToken), _options);
        if (!UpdateIntervals.IsDue(_releases.LastCheck?.CheckedAt, interval, _timeProvider.GetUtcNow()))
        {
            return false;
        }

        await CheckForUpdatesAsync(cancellationToken);
        return true;
    }

    private string? InstalledBinaryOrNull() => _versionProvider.IsInstalled ? Path.GetFullPath(_options.BinaryPath) : null;
}
