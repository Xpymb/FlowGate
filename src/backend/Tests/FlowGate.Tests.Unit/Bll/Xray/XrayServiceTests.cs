using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.SharedContracts.Dto.Xray;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты фасада <see cref="FlowGate.Bll.Implementations.Xray.XrayService"/>
/// </summary>
public sealed class XrayServiceTests : IDisposable
{
    private readonly XrayTestEnvironment _env = new();

    /// <summary>
    ///     Релизы без pre-release по умолчанию, с отметками «установлена» и «последняя»
    /// </summary>
    [Fact]
    public async Task GetReleasesAsync_InstalledOlderVersion_MarksInstalledAndLatest()
    {
        _env.InstallFakeBinary("26.10.21");

        var stable = await _env.Service.GetReleasesAsync(includePrerelease: false, refresh: false, TestContext.Current.CancellationToken);
        var all = await _env.Service.GetReleasesAsync(includePrerelease: true, refresh: false, TestContext.Current.CancellationToken);

        Assert.Equal(["26.10.31", "26.10.21"], stable.Select(release => release.Version));
        Assert.Equal(3, all.Count);
        Assert.True(stable[0].IsLatest);
        Assert.True(stable[1].IsInstalled);
        Assert.True(all.Single(release => release.Version == "26.9.30").IsPrerelease);
    }

    /// <summary>
    ///     После проверки обновлений статус показывает доступную версию
    /// </summary>
    [Fact]
    public async Task GetStatusAsync_AfterUpdateCheck_ReportsUpdateAvailable()
    {
        _env.InstallFakeBinary("26.10.21");
        await _env.Service.CheckForUpdatesAsync(TestContext.Current.CancellationToken);

        var status = await _env.Service.GetStatusAsync(TestContext.Current.CancellationToken);

        Assert.True(status.Installed);
        Assert.Equal("26.10.21", status.Version);
        Assert.Equal("26.10.31", status.LatestVersion);
        Assert.True(status.UpdateAvailable);
        Assert.Equal("Stopped", status.State);
    }

    /// <summary>
    ///     Старт и стоп меняют автозапуск
    /// </summary>
    [Fact]
    public async Task StartAndStop_InstalledXray_TogglesAutostartAndState()
    {
        _env.InstallFakeBinary("26.10.31");
        await _env.Settings.UpdateAsync(settings => settings with { XrayAutostart = false }, TestContext.Current.CancellationToken);

        var started = await _env.Service.StartAsync(TestContext.Current.CancellationToken);
        var stopped = await _env.Service.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Running", started.State);
        Assert.True(started.Autostart);
        Assert.Equal("Stopped", stopped.State);
        Assert.False(stopped.Autostart);
    }

    /// <summary>
    ///     При старте FlowGate установленный Xray с автозапуском запускается
    /// </summary>
    [Fact]
    public async Task InitializeAsync_InstalledWithAutostart_StartsXray()
    {
        _env.InstallFakeBinary("26.10.31");

        await _env.Service.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Single(_env.Launcher.Started);
        Assert.True(File.Exists(_env.ConfigFile.ConfigPath));
    }

    /// <summary>
    ///     Неверная строка версии отклоняется
    /// </summary>
    [Fact]
    public async Task InstallAsync_InvalidVersion_ThrowsValidation()
    {
        await Assert.ThrowsAsync<RequestValidationException>(
            () => _env.Service.InstallAsync(new XrayInstallRequest("latest-and-greatest"), UpdateVia.Auto, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public void Dispose() => _env.Dispose();
}
