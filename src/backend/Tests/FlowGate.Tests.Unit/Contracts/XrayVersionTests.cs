using FlowGate.Contracts.Xray;

namespace FlowGate.Tests.Unit.Contracts;

/// <summary>
///     Тесты разбора и сравнения <see cref="XrayVersion"/>
/// </summary>
public class XrayVersionTests
{
    /// <summary>
    ///     Версия разбирается из тега, числа и вывода xray version
    /// </summary>
    [Theory]
    [InlineData("v26.10.31")]
    [InlineData("26.10.31")]
    [InlineData("Xray 26.10.31 (Xray, Penetrates Everything.) a4a3dff (go1.27.3 linux/arm64)\nA unified platform for anti-censorship.")]
    public void TryParse_KnownFormats_ReturnsVersion(string value)
    {
        var parsed = XrayVersion.TryParse(value, out var version);

        Assert.True(parsed);
        Assert.Equal("26.10.31", version!.ToString());
    }

    /// <summary>
    ///     Мусор не разбирается
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v26")]
    [InlineData("26.x.1")]
    public void TryParse_Garbage_ReturnsFalse(string? value)
    {
        Assert.False(XrayVersion.TryParse(value, out _));
    }

    /// <summary>
    ///     Сравнение идёт по числам, а не по строкам
    /// </summary>
    [Fact]
    public void Compare_NumericSegments_OrdersNumerically()
    {
        var versions = new[] { "1.8.24", "26.10.31", "26.9.9", "26.10.21" }.Select(XrayVersion.Parse).ToList();

        var ordered = versions.Order().Select(version => version.ToString());

        Assert.Equal(["1.8.24", "26.9.9", "26.10.21", "26.10.31"], ordered);
        Assert.True(XrayVersion.Parse("26.10.31") > XrayVersion.Parse("26.9.30"));
    }

    /// <summary>
    ///     Хвостовые нули не влияют на равенство
    /// </summary>
    [Fact]
    public void Equals_TrailingZeroSegment_AreEqual()
    {
        var left = XrayVersion.Parse("26.10.31");
        var right = XrayVersion.Parse("26.10.31.0");

        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }
}
