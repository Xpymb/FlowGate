using FlowGate.Bll.Implementations.Xray;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Тесты <see cref="XrayBalancerInfoParser"/> на выводе <c>xray api bi -json</c>, снятом с роутера
/// </summary>
public sealed class XrayBalancerInfoParserTests
{
    /// <summary>
    ///     Два балансировщика: выбор сопоставляется с тегами по порядку аргументов
    /// </summary>
    [Fact]
    public void TryParse_RouterOutput_MapsSelectionsByArgumentOrder()
    {
        var parsed = XrayBalancerInfoParser.TryParse(XrayFixtures.BalancerInfo, ["bal-con-a", "bal-con-b"], out var active);

        Assert.True(parsed);
        Assert.Equal("out-con-a-1", active["bal-con-a"]);
        Assert.Equal("out-con-b", active["bal-con-b"]);
    }

    /// <summary>
    ///     Ручное закрепление важнее выбора стратегии; пустой выбор — null (Xray берёт fallbackTag)
    /// </summary>
    [Fact]
    public void TryParse_OverrideAndEmptySelection_ReadsBoth()
    {
        const string output = """
            {"balancer":{"override":{"target":"out-con-a-2"},"principleTarget":{"tag":["out-con-a"]}}}
            {"balancer":{"override":{},"principleTarget":{}}}
            """;

        var parsed = XrayBalancerInfoParser.TryParse(output, ["bal-con-a", "bal-con-b"], out var active);

        Assert.True(parsed);
        Assert.Equal("out-con-a-2", active["bal-con-a"]);
        Assert.Null(active["bal-con-b"]);
    }

    /// <summary>
    ///     Незнакомый вывод — отказ без исключения: колонка покажет «не поддерживается»
    /// </summary>
    /// <param name="output">Вывод команды</param>
    [Theory]
    [InlineData("")]
    [InlineData("  - Selects:\n    1   out-con-a-1")]
    [InlineData("{\"something\":{}}")]
    [InlineData("{\"balancer\":{\"principleTarget\":{\"tag\":\"out-con-a\"}}}")]
    [InlineData("{\"balancer\":{}}")]
    [InlineData("{\"balancer\":{}} {\"balancer\":{}} {\"balancer\":{}}")]
    [InlineData("{\"balancer\":")]
    public void TryParse_UnknownOutput_ReturnsFalse(string output)
    {
        var parsed = XrayBalancerInfoParser.TryParse(output, ["bal-con-a", "bal-con-b"], out var active);

        Assert.False(parsed);
        Assert.Empty(active);
    }
}
