using NSubstitute;

using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Exceptions;
using FlowGate.Shared.Keenetic;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Bll;

/// <summary>
///     Тесты <see cref="KeeneticSystemService"/>
/// </summary>
public class KeeneticSystemServiceTests
{
    private readonly IKeeneticRciClient _rciClient = Substitute.For<IKeeneticRciClient>();
    private readonly KeeneticSystemService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticSystemServiceTests"/>
    /// </summary>
    public KeeneticSystemServiceTests()
    {
        _sut = new KeeneticSystemService(_rciClient);
    }

    /// <summary>
    ///     Ответ реального Hopper SE разбирается и признаётся совместимым
    /// </summary>
    [Fact]
    public async Task GetSystemInfoAsync_RealHopperSe_ReturnsSupportedInfo()
    {
        _rciClient.GetAsync("show/version", Arg.Any<CancellationToken>())
            .Returns(RciFixtures.ToElement(RciFixtures.ShowVersion));

        var result = await _sut.GetSystemInfoAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Hopper SE (KN-3812)", result.Model);
        Assert.Equal("5.02.B.2.0-2", result.Release);
        Assert.Equal("5.2 Beta 2", result.Title);
        Assert.Equal("aarch64", result.Architecture);
        Assert.True(result.IsSupported);
        Assert.Empty(result.MissingComponents);
        Assert.Empty(result.Problems);
    }

    /// <summary>
    ///     Без списка компонентов все обязательные считаются отсутствующими
    /// </summary>
    [Fact]
    public async Task GetSystemInfoAsync_NoComponents_ReportsAllRequiredMissing()
    {
        _rciClient.GetAsync("show/version", Arg.Any<CancellationToken>())
            .Returns(RciFixtures.ToElement("""{"release":"5.02.B.2.0-2","arch":"aarch64","model":"X"}"""));

        var result = await _sut.GetSystemInfoAsync(TestContext.Current.CancellationToken);

        Assert.False(result.IsSupported);
        Assert.Equal(["proxy", "wireguard", "opkg"], result.MissingComponents);
    }

    /// <summary>
    ///     Ответ без release считается некорректным
    /// </summary>
    [Fact]
    public async Task GetSystemInfoAsync_NoRelease_ThrowsKeeneticUnavailable()
    {
        _rciClient.GetAsync("show/version", Arg.Any<CancellationToken>())
            .Returns(RciFixtures.ToElement("""{"model":"X"}"""));

        await Assert.ThrowsAsync<KeeneticUnavailableException>(
            () => _sut.GetSystemInfoAsync(TestContext.Current.CancellationToken));
    }
}
