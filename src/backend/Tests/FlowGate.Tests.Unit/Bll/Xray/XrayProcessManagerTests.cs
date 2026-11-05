using Microsoft.Extensions.Logging.Abstractions;

using FlowGate.Bll.Implementations.Xray;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Processes;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты <see cref="XrayProcessManager"/>: свои процессы известны, чужие процессы нашего бинарника убиваются
/// </summary>
public sealed class XrayProcessManagerTests : IDisposable
{
    private const string BinaryPath = "/opt/sbin/flowgate-xray";

    private readonly FakeProcessLauncher _launcher = new();
    private readonly FakeProcessTable _table = new();
    private readonly XrayProcessManager _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayProcessManagerTests"/>
    /// </summary>
    public XrayProcessManagerTests()
    {
        _sut = new XrayProcessManager(
            _launcher,
            _table,
            MsOptions.Create(new XrayOptions { BinaryPath = BinaryPath, WorkDirectory = Path.Combine(Path.GetTempPath(), "flowgate-tests", "pm") }),
            NullLogger<XrayProcessManager>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Убиваются процессы нашего бинарника (и подменённого), которых менеджер не запускал; свои и чужие бинарники — нет
    /// </summary>
    [Fact]
    public async Task KillStrangersAsync_UnknownOwnBinary_Killed()
    {
        var own = _sut.Start(["run", "-c", "xray.json"], _ => { });
        _table.Add(own.Id, Path.GetFullPath(BinaryPath));
        _table.Add(501, Path.GetFullPath(BinaryPath));
        _table.Add(502, Path.GetFullPath(BinaryPath) + " (deleted)");
        _table.Add(503, "/opt/sbin/other-xray");

        var killed = await _sut.KillStrangersAsync(Token);

        Assert.Equal(2, killed);
        Assert.Equal([501, 502], _table.Killed.Order());
    }

    /// <summary>
    ///     Завершившийся свой процесс забывается: если его pid занят нашим бинарником снова, это уже чужой
    /// </summary>
    [Fact]
    public async Task KillStrangersAsync_ExitedOwnProcess_Forgotten()
    {
        var own = _sut.Start(["run"], _ => { });
        _launcher.Started[0].Exit(0);
        await Task.Delay(20, Token);
        _table.Add(own.Id, Path.GetFullPath(BinaryPath));

        Assert.Equal(1, await _sut.KillStrangersAsync(Token));
    }

    /// <summary>
    ///     Остановка FlowGate убивает все свои процессы
    /// </summary>
    [Fact]
    public void Dispose_KillsOwnProcesses()
    {
        _sut.Start(["run"], _ => { });

        _sut.Dispose();

        Assert.True(_launcher.Started[0].WasKilled);
    }

    /// <inheritdoc />
    public void Dispose() => _sut.Dispose();
}
