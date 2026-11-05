using Microsoft.Extensions.Time.Testing;

using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты супервизора <see cref="FlowGate.Bll.Implementations.Xray.XrayProcessService"/> на подменном времени
/// </summary>
public sealed class XrayProcessServiceTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-11-04T12:00:00Z"));
    private readonly XrayTestEnvironment _env;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayProcessServiceTests"/>
    /// </summary>
    public XrayProcessServiceTests()
    {
        _env = new XrayTestEnvironment(_time);
        _env.InstallFakeBinary("26.10.31");
        Directory.CreateDirectory(Path.GetDirectoryName(_env.ConfigFile.ConfigPath)!);
        File.WriteAllText(_env.ConfigFile.ConfigPath, "{}");
    }

    /// <summary>
    ///     Запуск передаёт xray путь к конфигурации
    /// </summary>
    [Fact]
    public async Task StartAsync_Installed_RunsXrayWithConfig()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);

        var process = Assert.Single(_env.Launcher.Started);
        Assert.Equal(_env.BinaryPath, process.Request.FileName);
        Assert.Equal(["run", "-c", _env.ConfigFile.ConfigPath], process.Request.Arguments);
        var status = _env.Process.GetStatus();
        Assert.Equal(XrayProcessState.Running, status.State);
        Assert.Equal(process.Id, status.ProcessId);
    }

    /// <summary>
    ///     Без бинарника запуск невозможен
    /// </summary>
    [Fact]
    public async Task StartAsync_NotInstalled_ThrowsAndReportsNotInstalled()
    {
        File.Delete(_env.BinaryPath);

        await Assert.ThrowsAsync<XrayOperationException>(() => _env.Process.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(XrayProcessState.NotInstalled, _env.Process.GetStatus().State);
        Assert.Empty(_env.Launcher.Started);
    }

    /// <summary>
    ///     Процесс сразу завершился — запуск неудачен, код выхода сохраняется
    /// </summary>
    [Fact]
    public async Task StartAsync_ProcessExitsImmediately_ThrowsAndReportsFailed()
    {
        _env.Launcher.ExitOnStart = _ => 23;

        var exception = await Assert.ThrowsAsync<XrayOperationException>(() => _env.Process.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains("23", exception.Message);
        var status = _env.Process.GetStatus();
        Assert.Equal(XrayProcessState.Failed, status.State);
        Assert.Equal(23, status.LastExitCode);
    }

    /// <summary>
    ///     Неожиданное завершение — перезапуск через 1 с
    /// </summary>
    [Fact]
    public async Task Crash_UnexpectedExit_RestartsAfterOneSecond()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);

        _env.Launcher.Started[0].Exit(1);
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Process.GetStatus().State == XrayProcessState.Restarting);
        Assert.Single(_env.Launcher.Started);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Process.GetStatus().State == XrayProcessState.Running);

        Assert.Equal(2, _env.Launcher.Started.Count);
        var status = _env.Process.GetStatus();
        Assert.Equal(1, status.Restarts);
        Assert.Equal(1, status.LastExitCode);
    }

    /// <summary>
    ///     Повторное падение вскоре после перезапуска удваивает паузу
    /// </summary>
    [Fact]
    public async Task Crash_TwiceQuickly_DoublesBackoff()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);
        _env.Launcher.Started[0].Exit(1);
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Process.GetStatus().State == XrayProcessState.Restarting);
        await AdvanceAsync(TimeSpan.FromSeconds(1));
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Launcher.Started.Count == 2 && _env.Process.GetStatus().State == XrayProcessState.Running);

        _env.Launcher.Started[1].Exit(1);
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Process.GetStatus().State == XrayProcessState.Restarting);
        await AdvanceAsync(TimeSpan.FromSeconds(1));
        await Task.Delay(100, TestContext.Current.CancellationToken);
        Assert.Equal(2, _env.Launcher.Started.Count);

        await AdvanceAsync(TimeSpan.FromSeconds(1));
        await XrayTestEnvironment.WaitUntilAsync(() => _env.Launcher.Started.Count == 3);
    }

    /// <summary>
    ///     Остановка убивает процесс, супервизор его не перезапускает
    /// </summary>
    [Fact]
    public async Task StopAsync_Running_KillsAndDoesNotRestart()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);

        await _env.Process.StopAsync(TestContext.Current.CancellationToken);
        await AdvanceAsync(TimeSpan.FromMinutes(5));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.True(_env.Launcher.Started[0].WasKilled);
        Assert.Single(_env.Launcher.Started);
        Assert.Equal(XrayProcessState.Stopped, _env.Process.GetStatus().State);
    }

    /// <summary>
    ///     Перезапуск останавливает старый процесс и запускает новый
    /// </summary>
    [Fact]
    public async Task RestartAsync_Running_ReplacesProcess()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);

        await _env.Process.RestartAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, _env.Launcher.Started.Count);
        Assert.True(_env.Launcher.Started[0].WasKilled);
        Assert.Equal(_env.Launcher.Started[1].Id, _env.Process.GetStatus().ProcessId);
        Assert.Equal(0, _env.Process.GetStatus().Restarts);
    }

    /// <summary>
    ///     Вывод процесса попадает в буфер логов
    /// </summary>
    [Fact]
    public async Task Output_ProcessWritesLines_LinesInLogBuffer()
    {
        await _env.Process.StartAsync(TestContext.Current.CancellationToken);

        _env.Launcher.Started[0].WriteLine("[Warning] core: Xray 26.10.31 started");

        var line = Assert.Single(_env.Logs.Tail(10));
        Assert.Equal("[Warning] core: Xray 26.10.31 started", line.Text);
    }

    /// <inheritdoc />
    public void Dispose() => _env.Dispose();

    private async Task AdvanceAsync(TimeSpan by)
    {
        await Task.Delay(50, TestContext.Current.CancellationToken);
        _time.Advance(by);
    }
}
