using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты <see cref="FlowGate.Bll.Implementations.Xray.XrayInstaller"/>: установка, обновление, проверки и откат
/// </summary>
public sealed class XrayInstallerTests : IDisposable
{
    private readonly XrayTestEnvironment _env = new();

    /// <summary>
    ///     Установка последней стабильной версии кладёт бинарник, пишет конфигурацию и запускает Xray
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_LatestStable_InstallsAndStartsXray()
    {
        var job = await InstallAsync(null);

        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal("26.10.31", FakeXrayBinary.ReadVersion(_env.BinaryPath));
        Assert.True(File.Exists(_env.ConfigFile.ConfigPath));
        var process = Assert.Single(_env.Launcher.Started);
        Assert.Equal(["run", "-c", _env.ConfigFile.ConfigPath], process.Request.Arguments);
        Assert.Equal(XrayProcessState.Running, _env.Process.GetStatus().State);
        Assert.Contains(job.Steps, step => step.Message.Contains("SHA-256"));
        Assert.Empty(Directory.GetDirectories(_env.XrayOptions.WorkDirectory, "install-*"));
    }

    /// <summary>
    ///     Установка старой версии поверх новой оставляет резервную копию и меняет версию
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_OlderVersionOverInstalled_ReplacesBinaryAndKeepsBackup()
    {
        await InstallAsync(null);

        var job = await InstallAsync("26.10.21");

        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Equal("26.10.21", FakeXrayBinary.ReadVersion(_env.BinaryPath));
        Assert.Equal("26.10.31", FakeXrayBinary.ReadVersion(_env.BinaryPath + ".bak"));
        Assert.Equal("26.10.21", (await _env.VersionProvider.GetInstalledVersionAsync(TestContext.Current.CancellationToken))?.ToString());
        Assert.True(_env.Launcher.Started[0].WasKilled);
        Assert.Equal(XrayProcessState.Running, _env.Process.GetStatus().State);
    }

    /// <summary>
    ///     Неверная контрольная сумма — бинарник не подменяется
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_DigestMismatch_FailsWithoutInstalling()
    {
        _env.GitHub.DigestOverride = _ => $"SHA2-256= {new string('0', 64)}";

        var job = await InstallAsync(null);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("Контрольная сумма", job.Error);
        Assert.False(File.Exists(_env.BinaryPath));
        Assert.Empty(_env.Launcher.Started);
    }

    /// <summary>
    ///     Бинарник в архиве другой версии — установка отклоняется
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_ArchiveContainsOtherVersion_FailsWithoutInstalling()
    {
        _env.GitHub.BinaryContent = _ => FakeXrayBinary.Content("1.8.24");

        var job = await InstallAsync(null);

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("1.8.24", job.Error);
        Assert.False(File.Exists(_env.BinaryPath));
    }

    /// <summary>
    ///     Новая версия не стартует — возвращается прежняя и снова запускается
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_NewVersionFailsToStart_RollsBackToPrevious()
    {
        await InstallAsync("26.10.21");
        _env.Launcher.ExitOnStart = request => FakeXrayBinary.ReadVersion(request.FileName) == "26.10.31" ? 23 : null;

        var job = await InstallAsync("26.10.31");

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("откат", job.Error);
        Assert.Equal("26.10.21", FakeXrayBinary.ReadVersion(_env.BinaryPath));
        Assert.Equal(XrayProcessState.Running, _env.Process.GetStatus().State);
        Assert.Equal(3, _env.Launcher.Started.Count);
    }

    /// <summary>
    ///     С выключенным автозапуском остановленный Xray после установки не запускается
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_AutostartDisabled_DoesNotStartXray()
    {
        await _env.Settings.UpdateAsync(settings => settings with { XrayAutostart = false }, TestContext.Current.CancellationToken);

        var job = await InstallAsync(null);

        Assert.Equal(JobStatus.Succeeded, job.Status);
        Assert.Empty(_env.Launcher.Started);
        Assert.Equal(XrayProcessState.Stopped, _env.Process.GetStatus().State);
    }

    /// <summary>
    ///     Несуществующая версия отклоняется до запуска задачи
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_UnknownVersion_ThrowsEntityNotFound()
    {
        await Assert.ThrowsAsync<EntityNotFoundException>(
            () => _env.Installer.StartInstallAsync(XrayVersion.Parse("1.0.0"), TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Вторая установка во время первой отклоняется
    /// </summary>
    [Fact]
    public async Task StartInstallAsync_WhileInstalling_ThrowsConflict()
    {
        using var gate = new ManualResetEventSlim(false);
        _env.GitHub.DownloadGate = gate;
        var first = await _env.Installer.StartInstallAsync(null, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ConflictException>(
            () => _env.Installer.StartInstallAsync(null, TestContext.Current.CancellationToken));

        gate.Set();
        var finished = await _env.Jobs.WaitAsync(first.Id, TestContext.Current.CancellationToken);
        Assert.Equal(JobStatus.Succeeded, finished!.Status);
    }

    /// <inheritdoc />
    public void Dispose() => _env.Dispose();

    private async Task<JobSnapshot> InstallAsync(string? version)
    {
        var started = await _env.Installer.StartInstallAsync(
            version is null ? null : XrayVersion.Parse(version),
            TestContext.Current.CancellationToken);

        return (await _env.Jobs.WaitAsync(started.Id, TestContext.Current.CancellationToken))!;
    }
}
