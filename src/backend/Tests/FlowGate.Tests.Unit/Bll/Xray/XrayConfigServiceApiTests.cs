using System.Text;

using NSubstitute;

using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты отката <see cref="FlowGate.Bll.Implementations.Xray.XrayConfigService"/> к конфигу без API, если
///     установленный Xray не принимает блок <c>api</c>
/// </summary>
public sealed class XrayConfigServiceApiTests : IDisposable
{
    private const string TestBinary = "xray-under-test";

    private readonly XrayTestEnvironment _env = new();

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayConfigServiceApiTests"/>
    /// </summary>
    public XrayConfigServiceApiTests()
    {
        _env.ConfigSource
            .GetModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => XrayConfigModel.Empty(call.Arg<string>()) with { Api = new XrayApi("127.0.0.1:10792", ["RoutingService"]) });
    }

    /// <summary>
    ///     Xray принимает API — конфиг с API, состояние Enabled
    /// </summary>
    [Fact]
    public async Task WriteAsync_ApiAccepted_WritesApi()
    {
        var changed = await _env.ConfigService.WriteAsync(TestBinary, Token);

        Assert.True(changed);
        Assert.Contains("\"api\"", await ReadConfigAsync());
        Assert.Equal(XrayApiAvailability.Enabled, _env.ApiState.Availability);
    }

    /// <summary>
    ///     Xray отклоняет API — записан конфиг без API, состояние Rejected; повтор ничего не меняет
    /// </summary>
    [Fact]
    public async Task WriteAsync_ApiRejected_FallsBackWithoutApi()
    {
        RejectApi();

        var changed = await _env.ConfigService.WriteAsync(TestBinary, Token);
        var repeated = await _env.ConfigService.WriteAsync(TestBinary, Token);

        Assert.True(changed);
        Assert.False(repeated);
        Assert.DoesNotContain("\"api\"", await ReadConfigAsync());
        Assert.Equal(XrayApiAvailability.Rejected, _env.ApiState.Availability);
    }

    /// <summary>
    ///     Новая версия Xray принимает API — конфиг без API заменяется конфигом с API
    /// </summary>
    [Fact]
    public async Task WriteAsync_ApiAcceptedAfterRejection_RestoresApi()
    {
        RejectApi();
        await _env.ConfigService.WriteAsync(TestBinary, Token);
        _env.Runner.Handler = (_, _) => new ProcessResult(0, "Configuration OK.", string.Empty);

        var changed = await _env.ConfigService.WriteAsync(TestBinary, Token);

        Assert.True(changed);
        Assert.Equal(XrayApiAvailability.Enabled, _env.ApiState.Availability);
    }

    /// <summary>
    ///     Xray отклоняет оба варианта — ошибка, действующий файл не меняется
    /// </summary>
    [Fact]
    public async Task WriteAsync_BothRejected_Throws()
    {
        _env.Runner.Handler = (_, _) => new ProcessResult(23, "Failed to start: bad config", string.Empty);

        await Assert.ThrowsAsync<XrayOperationException>(() => _env.ConfigService.WriteAsync(TestBinary, Token));

        Assert.False(File.Exists(_env.ConfigFile.ConfigPath));
    }

    /// <summary>
    ///     Модель без API — проверяется один вариант, состояние NotNeeded
    /// </summary>
    [Fact]
    public async Task WriteAsync_NoApi_SingleTest()
    {
        _env.ConfigSource
            .GetModelAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => XrayConfigModel.Empty(call.Arg<string>()));

        await _env.ConfigService.WriteAsync(TestBinary, Token);

        Assert.Single(_env.Runner.Calls);
        Assert.Equal(XrayApiAvailability.NotNeeded, _env.ApiState.Availability);
    }

    /// <inheritdoc />
    public void Dispose() => _env.Dispose();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private void RejectApi() =>
        _env.Runner.Handler = (_, arguments) => File.ReadAllText(arguments[^1]).Contains("\"api\"", StringComparison.Ordinal)
            ? new ProcessResult(23, "Failed to start: main: failed to load config files > unknown field \"api\"", string.Empty)
            : new ProcessResult(0, "Configuration OK.", string.Empty);

    private async Task<string> ReadConfigAsync() =>
        Encoding.UTF8.GetString((await _env.ConfigFile.ReadAsync(Token))!);
}
