using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Jobs;
using FlowGate.Bll.Interfaces.Maintenance;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Maintenance;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Maintenance;

namespace FlowGate.Bll.Implementations.Maintenance;

/// <summary>
///     Состояние FlowGate, проверка релизов на GitHub и самообновление скриптом пакета
/// </summary>
public sealed class SystemService : ISystemService
{
    /// <summary>
    ///     Вид фоновой задачи самообновления
    /// </summary>
    public const string JobKind = "system-upgrade";

    /// <summary>
    ///     Pid-файл отвязанного процесса обновления: по нему <c>start-stop-daemon</c> ищет уже запущенный экземпляр
    /// </summary>
    public const string UpgradePidFile = "/tmp/flowgate-upgrade.pid";

    private const int ReleasesToCheck = 10;
    private static readonly TimeSpan LaunchTimeout = TimeSpan.FromSeconds(15);

    private readonly IGitHubReleasesClient _gitHub;
    private readonly IXrayProcessService _xray;
    private readonly IJobRunner _jobs;
    private readonly IProcessRunner _processRunner;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SystemService> _logger;
    private readonly SystemOptions _options;
    private readonly Version? _currentVersion = AppUpdateCheck.ParseVersion(AppVersion.Current);
    private readonly DateTimeOffset _startedAt;
    private AppUpdateCheck? _lastCheck;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SystemService"/>
    /// </summary>
    /// <param name="gitHub">Клиент релизов GitHub</param>
    /// <param name="xray">Процесс Xray (pid для памяти)</param>
    /// <param name="jobs">Фоновые задачи</param>
    /// <param name="processRunner">Запуск команд</param>
    /// <param name="options">Настройки самообновления</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SystemService(
        IGitHubReleasesClient gitHub,
        IXrayProcessService xray,
        IJobRunner jobs,
        IProcessRunner processRunner,
        IOptions<SystemOptions> options,
        TimeProvider timeProvider,
        ILogger<SystemService> logger)
    {
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        _xray = xray ?? throw new ArgumentNullException(nameof(xray));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _startedAt = ProcessStartedAt() ?? _timeProvider.GetUtcNow();
    }

    /// <inheritdoc />
    public async Task<SystemStatusDto> GetStatusAsync(CancellationToken cancellationToken)
    {
        var lastCheck = _lastCheck;
        var xrayPid = _xray.GetStatus().ProcessId;
        var uptime = _timeProvider.GetUtcNow() - _startedAt;

        return new SystemStatusDto(
            AppVersion.Current,
            lastCheck?.LatestVersion?.ToString(),
            lastCheck?.IsNewerThan(_currentVersion) ?? false,
            lastCheck?.CheckedAt,
            File.Exists(_options.UpgradeScriptPath),
            await ReadLastUpgradeResultAsync(cancellationToken),
            _startedAt,
            Math.Max(0, (long)uptime.TotalSeconds),
            await ReadRssAsync("self", cancellationToken),
            xrayPid is { } pid ? await ReadRssAsync(pid.ToString(CultureInfo.InvariantCulture), cancellationToken) : null);
    }

    /// <inheritdoc />
    public async Task<AppUpdateCheck> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        var releases = await _gitHub.GetReleasesAsync(_options.Repository, ReleasesToCheck, cancellationToken);
        var check = AppUpdateCheck.FromReleases(
            releases.Select(release => (release.TagName, release.Draft, release.Prerelease)),
            _timeProvider.GetUtcNow());

        if (check.IsNewerThan(_currentVersion) && check.LatestVersion != _lastCheck?.LatestVersion)
        {
            _logger.LogInformation("Доступна новая версия FlowGate {Version}", check.LatestVersion);
        }

        _lastCheck = check;
        return check;
    }

    /// <inheritdoc />
    public JobDto RequestUpgrade()
    {
        if (!File.Exists(_options.UpgradeScriptPath))
        {
            throw new ConflictException("FlowGate установлен не пакетом opkg: самообновление недоступно");
        }

        return _jobs.Start(JobKind, "Обновление FlowGate", LaunchUpgradeAsync).ToDto();
    }

    private async Task LaunchUpgradeAsync(IJobContext context, CancellationToken cancellationToken)
    {
        context.Report("Запуск opkg upgrade flowgate");
        var result = await _processRunner.RunAsync(
            _options.StartStopDaemonPath,
            ["-S", "-b", "-m", "-p", UpgradePidFile, "-x", "/bin/sh", "--", _options.UpgradeScriptPath],
            LaunchTimeout,
            cancellationToken);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Не удалось запустить обновление (код {result.ExitCode}): {result.StandardError.Trim()}");
        }

        _logger.LogInformation("Запущено обновление FlowGate {Version} через opkg", AppVersion.Current);
        context.Report("Обновление запущено: FlowGate перезапустится, итог появится в состоянии системы");
    }

    private async Task<string?> ReadLastUpgradeResultAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_options.UpgradeLogPath))
            {
                return null;
            }

            var lines = await File.ReadAllLinesAsync(_options.UpgradeLogPath, cancellationToken);
            return lines.LastOrDefault(line => line.StartsWith("ok ", StringComparison.Ordinal)
                || line.StartsWith("failed ", StringComparison.Ordinal));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static async Task<long?> ReadRssAsync(string process, CancellationToken cancellationToken)
    {
        var path = $"/proc/{process}/status";
        try
        {
            return File.Exists(path) ? ProcStatus.ParseRssKilobytes(await File.ReadAllTextAsync(path, cancellationToken)) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTimeOffset? ProcessStartedAt()
    {
        try
        {
            using var current = Process.GetCurrentProcess();
            return new DateTimeOffset(current.StartTime.ToUniversalTime(), TimeSpan.Zero);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or Win32Exception)
        {
            return null;
        }
    }
}
