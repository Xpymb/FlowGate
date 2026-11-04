using FlowGate.Contracts.Keenetic;

namespace FlowGate.Tests.Unit.Contracts;

/// <summary>
///     Тесты правил совместимости <see cref="KeeneticSystemInfo"/>
/// </summary>
public class KeeneticSystemInfoTests
{
    private static readonly string[] AllComponents = ["base", "proxy", "wireguard", "opkg", "ndns"];

    /// <summary>
    ///     KeeneticOS 5.2 на aarch64 со всеми компонентами подходит
    /// </summary>
    [Fact]
    public void IsSupported_KeeneticOs5OnAarch64WithComponents_ReturnsTrue()
    {
        var sut = new KeeneticSystemInfo("Hopper SE (KN-3812)", "5.02.B.2.0-2", "5.2 Beta 2", "aarch64", AllComponents);

        Assert.True(sut.IsSupported);
        Assert.Equal(5, sut.MajorVersion);
        Assert.Empty(sut.Problems);
    }

    /// <summary>
    ///     KeeneticOS 4 не подходит, причина попадает в Problems
    /// </summary>
    [Fact]
    public void IsSupported_KeeneticOs4_ReturnsFalseWithVersionProblem()
    {
        var sut = new KeeneticSystemInfo("Giga", "4.03.C.6.0-0", "4.3.6", "aarch64", AllComponents);

        Assert.False(sut.IsSupported);
        var problem = Assert.Single(sut.Problems);
        Assert.Contains("KeeneticOS 5", problem);
    }

    /// <summary>
    ///     Не aarch64 — не подходит
    /// </summary>
    [Fact]
    public void IsSupported_MipsArchitecture_ReturnsFalse()
    {
        var sut = new KeeneticSystemInfo("Viva", "5.00.C.1.0-0", "5.0.1", "mips", AllComponents);

        Assert.False(sut.IsArchitectureSupported);
        Assert.False(sut.IsSupported);
    }

    /// <summary>
    ///     Отсутствующие обязательные компоненты перечисляются
    /// </summary>
    [Fact]
    public void MissingComponents_NoWireguardAndProxy_ReportsBoth()
    {
        var sut = new KeeneticSystemInfo("Hopper", "5.01.A.1.0-0", "5.1", "aarch64", ["base", "opkg"]);

        Assert.Equal(["proxy", "wireguard"], sut.MissingComponents);
        Assert.Equal(2, sut.Problems.Count);
        Assert.False(sut.IsSupported);
    }

    /// <summary>
    ///     Неразбираемая версия считается неподдерживаемой
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("beta")]
    public void MajorVersion_UnparsableRelease_IsNullAndUnsupported(string release)
    {
        var sut = new KeeneticSystemInfo("X", release, "?", "aarch64", AllComponents);

        Assert.Null(sut.MajorVersion);
        Assert.False(sut.IsVersionSupported);
    }
}
