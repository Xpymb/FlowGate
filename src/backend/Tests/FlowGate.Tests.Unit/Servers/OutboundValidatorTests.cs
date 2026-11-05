using System.Text.Json.Nodes;

using NSubstitute;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Processes;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Тесты <see cref="OutboundValidator"/>
/// </summary>
public sealed class OutboundValidatorTests : IDisposable
{
    private readonly string _workDirectory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly IXrayVersionProvider _versionProvider = Substitute.For<IXrayVersionProvider>();
    private readonly FakeProcessRunner _runner = new();
    private readonly OutboundValidator _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="OutboundValidatorTests"/>
    /// </summary>
    public OutboundValidatorTests()
    {
        _versionProvider.IsInstalled.Returns(true);
        _sut = new OutboundValidator(
            _versionProvider,
            _runner,
            Microsoft.Extensions.Options.Options.Create(new XrayOptions { BinaryPath = "/opt/sbin/xray", WorkDirectory = _workDirectory }));
    }

    /// <summary>
    ///     Структурные ошибки находятся без запуска Xray
    /// </summary>
    [Fact]
    public async Task ValidateAsync_BrokenStructure_ReturnsErrorsWithoutXray()
    {
        var outbound = new JsonObject { ["protocol"] = "mtproto", ["proxySettings"] = new JsonObject { ["tag"] = "x" } };

        var result = await _sut.ValidateAsync(outbound, TestContext.Current.CancellationToken);

        Assert.False(result.CheckedByXray);
        Assert.Contains(result.Errors, error => error.StartsWith("/protocol"));
        Assert.Contains(result.Errors, error => error.StartsWith("/settings"));
        Assert.Contains(result.Errors, error => error.Contains("цепочки"));
        Assert.Empty(_runner.Calls);
    }

    /// <summary>
    ///     Корректный outbound проверяется xray run -test во временном конфиге с тегом validate; файл удаляется
    /// </summary>
    [Fact]
    public async Task ValidateAsync_XrayAccepts_ValidAndTempFileRemoved()
    {
        string? config = null;
        _runner.Handler = (_, arguments) =>
        {
            config = File.ReadAllText(arguments[^1]);
            return new ProcessResult(0, "Configuration OK.", string.Empty);
        };

        var result = await _sut.ValidateAsync(TrojanOutbound(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.True(result.CheckedByXray);
        var call = Assert.Single(_runner.Calls);
        Assert.Equal(["run", "-test", "-c"], call.Arguments.Take(3));
        Assert.Contains("\"tag\":\"validate\"", config);
        Assert.Empty(Directory.GetFiles(_workDirectory));
    }

    /// <summary>
    ///     Ошибка xray превращается в понятные строки: без баннера, отметок времени и пути временного файла
    /// </summary>
    [Fact]
    public async Task ValidateAsync_XrayRejects_ReturnsCleanErrors()
    {
        _runner.Handler = (_, arguments) => new ProcessResult(
            23,
            "Xray 26.10.31 (Xray, Penetrates Everything.) a4a3dff (go1.27.3 linux/arm64)\nA unified platform for anti-censorship.\n"
            + $"2026/11/04 18:16:40.786545 [Info] infra/conf/serial: Reading config: &{{Name:{arguments[^1]} Format:json}}\n"
            + $"Failed to start: main: failed to load config files: [{arguments[^1]}] > infra/conf: failed to build outbound config with tag validate > infra/conf: unknown field \"probeUrl\"",
            string.Empty);

        var result = await _sut.ValidateAsync(TrojanOutbound(), TestContext.Current.CancellationToken);

        var error = Assert.Single(result.Errors);
        Assert.Equal("failed to build outbound config with tag validate → unknown field \"probeUrl\"", error);
        Assert.DoesNotContain(_workDirectory, error);
    }

    /// <summary>
    ///     Реальные ошибки Xray 26 с роутера сокращаются до двух последних звеньев
    /// </summary>
    [Theory]
    [InlineData(
        "Failed to start: main: failed to load config files: [outbound] > infra/conf: failed to build outbound config with tag validate > infra/conf: failed to build stream settings for outbound detour > infra/conf: Failed to build REALITY config. > infra/conf: empty \"password\"",
        "Failed to build REALITY config. → empty \"password\"")]
    [InlineData(
        "Failed to start: main: failed to load config files: [outbound] > infra/conf: failed to build outbound config with tag validate > infra/conf: failed to build stream settings for outbound detour > infra/conf: Config: unknown transport protocol: kcpx",
        "failed to build stream settings for outbound detour → unknown transport protocol: kcpx")]
    public void Shorten_RealXrayErrors_KeepsLastTwoLinks(string error, string expected)
    {
        Assert.Equal(expected, OutboundValidator.Shorten(error));
    }

    /// <summary>
    ///     Без установленного Xray — только структурная проверка
    /// </summary>
    [Fact]
    public async Task ValidateAsync_XrayNotInstalled_StructuralOnly()
    {
        _versionProvider.IsInstalled.Returns(false);

        var result = await _sut.ValidateAsync(TrojanOutbound(), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.False(result.CheckedByXray);
        Assert.Empty(_runner.Calls);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_workDirectory))
        {
            Directory.Delete(_workDirectory, recursive: true);
        }
    }

    private static JsonObject TrojanOutbound() =>
        new()
        {
            ["protocol"] = "trojan",
            ["settings"] = new JsonObject { ["address"] = "a.example", ["port"] = 443, ["password"] = "p" },
        };
}
