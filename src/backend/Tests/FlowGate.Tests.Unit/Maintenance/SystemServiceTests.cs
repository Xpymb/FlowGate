using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using FlowGate.Bll.Implementations.Maintenance;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.GitHub.Models;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Processes;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Maintenance;

/// <summary>
///     Тесты <see cref="SystemService"/>: проверка релизов, состояние, запуск самообновления
/// </summary>
public sealed class SystemServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly IGitHubReleasesClient _gitHub = Substitute.For<IGitHubReleasesClient>();
    private readonly IXrayProcessService _xray = Substitute.For<IXrayProcessService>();
    private readonly FakeProcessRunner _processRunner = new();
    private readonly ISettingsProvider _settings = Substitute.For<ISettingsProvider>();
    private readonly IUpdateProxyService _updateProxy = Substitute.For<IUpdateProxyService>();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly JobRunner _jobs;
    private readonly SystemOptions _options;
    private readonly SystemService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SystemServiceTests"/>
    /// </summary>
    public SystemServiceTests()
    {
        Directory.CreateDirectory(_directory);
        _jobs = new JobRunner(_time, NullLogger<JobRunner>.Instance);
        _options = new SystemOptions
        {
            Repository = "Xpymb/FlowGate",
            UpgradeScriptPath = Path.Combine(_directory, "upgrade.sh"),
            UpgradeLogPath = Path.Combine(_directory, "upgrade.log"),
            StartStopDaemonPath = "/opt/sbin/start-stop-daemon",
        };
        _xray.GetStatus().Returns(new XrayProcessStatus(XrayProcessState.Stopped, null, null, 0, null, null));
        _processRunner.Handler = (_, _) => new ProcessResult(0, "", "");
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(AppSettings.Default);
        _sut = new SystemService(
            _gitHub,
            _xray,
            _jobs,
            _processRunner,
            _settings,
            _updateProxy,
            MsOptions.Create(_options),
            _time,
            NullLogger<SystemService>.Instance);
    }

    /// <summary>
    ///     Проверка видит новую стабильную версию: состояние показывает доступное обновление
    /// </summary>
    [Fact]
    public async Task CheckForUpdatesAsync_NewerRelease_StatusShowsUpdate()
    {
        _gitHub.GetReleasesAsync("Xpymb/FlowGate", Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([Release("v99.0.0"), Release("v100.0.0", prerelease: true)]);

        var check = await _sut.CheckForUpdatesAsync(Token);
        var status = await _sut.GetStatusAsync(Token);

        Assert.Equal(new Version(99, 0, 0), check.LatestVersion);
        Assert.Equal("99.0.0", status.LatestVersion);
        Assert.True(status.UpdateAvailable);
        Assert.Equal(_time.GetUtcNow(), status.UpdateCheckedAt);
    }

    /// <summary>
    ///     Без проверки и без скрипта пакета: обновления нет, самообновление недоступно
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_NotPackaged_NoUpdateAndNotInstalled()
    {
        var status = await _sut.GetStatusAsync(Token);

        Assert.False(status.UpdateAvailable);
        Assert.Null(status.LatestVersion);
        Assert.False(status.PackageInstalled);
        Assert.Null(status.LastUpgradeResult);
    }

    /// <summary>
    ///     Итог последнего обновления — последняя строка <c>ok</c>/<c>failed</c> журнала
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_UpgradeLog_ReturnsLastResult()
    {
        await File.WriteAllTextAsync(_options.UpgradeLogPath, "ok 0.1.0\n2026-11-05 обновление\nUpgrading flowgate...\nfailed opkg update не выполнен\n", Token);

        var status = await _sut.GetStatusAsync(Token);

        Assert.Equal("failed opkg update не выполнен", status.LastUpgradeResult);
    }

    /// <summary>
    ///     Без скрипта пакета обновление отклоняется 409
    /// </summary>
    [Fact]
    public void RequestUpgrade_NotPackaged_Conflict()
    {
        Assert.Throws<ConflictException>(() => _sut.RequestUpgrade());
        Assert.Empty(_processRunner.Calls);
    }

    /// <summary>
    ///     Со скриптом пакета обновление запускается отвязанным процессом через start-stop-daemon
    /// </summary>
    [Fact]
    public async Task RequestUpgrade_Packaged_LaunchesDetachedScript()
    {
        await File.WriteAllTextAsync(_options.UpgradeScriptPath, "#!/bin/sh\n", Token);

        var job = _sut.RequestUpgrade();
        var finished = await _jobs.WaitAsync(job.Id, Token);

        Assert.Null(finished!.Error);
        var call = Assert.Single(_processRunner.Calls);
        Assert.Equal("/opt/sbin/start-stop-daemon", call.FileName);
        Assert.Equal(["-S", "-b", "-m", "-p", SystemService.UpgradePidFile, "-x", "/bin/sh", "--", _options.UpgradeScriptPath], call.Arguments);
    }

    /// <summary>
    ///     Прокси обновлений включён — скрипт получает бинарник Xray, конфиг и порт отдельного экземпляра
    /// </summary>
    [Fact]
    public async Task RequestUpgrade_UpdateProxyEnabled_PassesProxyArguments()
    {
        await File.WriteAllTextAsync(_options.UpgradeScriptPath, "#!/bin/sh\n", Token);
        _updateProxy.PrepareStandaloneAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateProxyStandaloneConfig("/opt/sbin/flowgate-xray", "/tmp/flowgate/update-proxy-upgrade.json", 10791));

        var job = _sut.RequestUpgrade();
        await _jobs.WaitAsync(job.Id, Token);

        var call = Assert.Single(_processRunner.Calls);
        Assert.Equal(
            ["--xray", "/opt/sbin/flowgate-xray", "--proxy-config", "/tmp/flowgate/update-proxy-upgrade.json", "--proxy-port", "10791"],
            call.Arguments.Skip(9));
    }

    /// <summary>
    ///     Проверка по расписанию: интервал из настроек ещё не истёк — GitHub не спрашивается
    /// </summary>
    [Fact]
    public async Task CheckForUpdatesIfDueAsync_IntervalFromSettings_SkipsUntilDue()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(AppSettings.Default with { Intervals = new UpdateIntervalSettings { SystemUpdateCheckHours = 2 } });
        _gitHub.GetReleasesAsync("Xpymb/FlowGate", Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([Release("v99.0.0")]);

        Assert.True(await _sut.CheckForUpdatesIfDueAsync(Token));
        _time.Advance(TimeSpan.FromHours(1));
        Assert.False(await _sut.CheckForUpdatesIfDueAsync(Token));
        _time.Advance(TimeSpan.FromHours(1));
        Assert.True(await _sut.CheckForUpdatesIfDueAsync(Token));

        await _gitHub.Received(2).GetReleasesAsync("Xpymb/FlowGate", Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     start-stop-daemon вернул ошибку — задача завершается ошибкой
    /// </summary>
    [Fact]
    public async Task RequestUpgrade_LaunchFails_JobFailed()
    {
        await File.WriteAllTextAsync(_options.UpgradeScriptPath, "#!/bin/sh\n", Token);
        _processRunner.Handler = (_, _) => new ProcessResult(1, "", "start-stop-daemon: not found");

        var job = _sut.RequestUpgrade();
        var finished = await _jobs.WaitAsync(job.Id, Token);

        Assert.Contains("not found", finished!.Error);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static GitHubRelease Release(string tag, bool prerelease = false) =>
        new(tag, tag, Draft: false, prerelease, DateTimeOffset.UnixEpoch, []);
}
