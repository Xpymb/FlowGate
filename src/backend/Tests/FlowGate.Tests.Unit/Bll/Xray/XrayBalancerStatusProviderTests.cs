using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Processes;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты <see cref="XrayBalancerStatusProvider"/>: <c>xray api bi</c> подменён, разные версии Xray имитируются его ответами
/// </summary>
public sealed class XrayBalancerStatusProviderTests : IDisposable
{
    private static readonly string[] Tags = ["bal-con-a", "bal-con-b"];

    private readonly XrayApiState _apiState = new() { Availability = XrayApiAvailability.Enabled };
    private readonly IXrayProcessService _process = Substitute.For<IXrayProcessService>();
    private readonly IXrayVersionProvider _versionProvider = Substitute.For<IXrayVersionProvider>();
    private readonly FakeProcessRunner _runner = new();
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-11-21T12:00:00Z"));
    private readonly XrayBalancerStatusProvider _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayBalancerStatusProviderTests"/>
    /// </summary>
    public XrayBalancerStatusProviderTests()
    {
        _versionProvider.IsInstalled.Returns(true);
        _process.GetStatus().Returns(new XrayProcessStatus(XrayProcessState.Running, 1, null, 0, null, null));
        _runner.Handler = (_, _) => new ProcessResult(0, XrayFixtures.BalancerInfo, string.Empty);

        _sut = new XrayBalancerStatusProvider(
            _apiState,
            _process,
            _versionProvider,
            _runner,
            Microsoft.Extensions.Options.Options.Create(new XrayOptions { ApiPort = 10792 }),
            _time,
            NullLogger<XrayBalancerStatusProvider>.Instance);
    }

    /// <summary>
    ///     Xray отвечает знакомым форматом — выбор по каждому балансировщику, команда с тегами и адресом API
    /// </summary>
    [Fact]
    public async Task GetAsync_KnownOutput_ReturnsSelections()
    {
        var status = await _sut.GetAsync(Tags, Token);

        Assert.Equal(XrayBalancerSupport.Supported, status.Support);
        Assert.Equal("out-con-a-1", status.ActiveTags["bal-con-a"]);
        Assert.Equal(["api", "bi", "-s", "127.0.0.1:10792", "-json", "bal-con-a", "bal-con-b"], Assert.Single(_runner.Calls).Arguments);
    }

    /// <summary>
    ///     Повтор в течение 10 с берётся из кеша, позже — снова из Xray
    /// </summary>
    [Fact]
    public async Task GetAsync_Repeated_UsesCacheForTenSeconds()
    {
        await _sut.GetAsync(Tags, Token);
        _time.Advance(TimeSpan.FromSeconds(9));
        await _sut.GetAsync(Tags, Token);
        _time.Advance(TimeSpan.FromSeconds(2));
        await _sut.GetAsync(Tags, Token);

        Assert.Equal(2, _runner.Calls.Count);
    }

    /// <summary>
    ///     Версия Xray без команды, с другим форматом или без порта API — «не поддерживается», без исключения
    /// </summary>
    /// <param name="exitCode">Код выхода</param>
    /// <param name="output">Вывод</param>
    [Theory]
    [InlineData(2, "flag provided but not defined: -json")]
    [InlineData(1, "xray: unknown command \"api bi\"")]
    [InlineData(1, "set balancer tag\nfailed to dial 127.0.0.1:10792")]
    [InlineData(0, "  - Selects:\n    1   out-con-a-1")]
    public async Task GetAsync_OtherXrayVersion_ReturnsUnsupported(int exitCode, string output)
    {
        _runner.Handler = (_, _) => new ProcessResult(exitCode, output, string.Empty);

        var status = await _sut.GetAsync(Tags, Token);

        Assert.Equal(XrayBalancerSupport.Unsupported, status.Support);
    }

    /// <summary>
    ///     Конфиг меняется (Xray ещё не знает тег) или команда зависла — «недоступно»
    /// </summary>
    [Fact]
    public async Task GetAsync_TagMissingOrTimeout_ReturnsUnavailable()
    {
        _runner.Handler = (_, _) => new ProcessResult(1, "failed to get health information: rpc error: code = Unknown desc = app/router: cannot find tag", string.Empty);
        var missing = await _sut.GetAsync(Tags, Token);

        _time.Advance(TimeSpan.FromMinutes(1));
        _runner.Handler = (_, _) => throw new TimeoutException("timeout");
        var timedOut = await _sut.GetAsync(Tags, Token);

        Assert.Equal(XrayBalancerSupport.Unavailable, missing.Support);
        Assert.Equal(XrayBalancerSupport.Unavailable, timedOut.Support);
    }

    /// <summary>
    ///     Xray отклонил конфиг с API — «не поддерживается» без запуска команды
    /// </summary>
    [Fact]
    public async Task GetAsync_ApiRejected_ReturnsUnsupportedWithoutCall()
    {
        _apiState.Availability = XrayApiAvailability.Rejected;

        var status = await _sut.GetAsync(Tags, Token);

        Assert.Equal(XrayBalancerSupport.Unsupported, status.Support);
        Assert.Empty(_runner.Calls);
    }

    /// <summary>
    ///     Xray не запущен — «недоступно»; балансировщиков нет — спрашивать нечего
    /// </summary>
    [Fact]
    public async Task GetAsync_NotRunningOrNoBalancers_SkipsCall()
    {
        var none = await _sut.GetAsync([], Token);
        _process.GetStatus().Returns(new XrayProcessStatus(XrayProcessState.Stopped, null, null, 0, null, null));
        var stopped = await _sut.GetAsync(Tags, Token);

        Assert.Equal(XrayBalancerSupport.NotNeeded, none.Support);
        Assert.Equal(XrayBalancerSupport.Unavailable, stopped.Support);
        Assert.Empty(_runner.Calls);
    }

    /// <inheritdoc />
    public void Dispose() => _sut.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
