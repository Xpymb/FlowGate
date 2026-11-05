using NSubstitute;

using FlowGate.Bll.Implementations.Connections;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Processes;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="XrayWireguardKeyGenerator"/>: разбор вывода <c>xray wg</c> и отказ без установленного Xray
/// </summary>
public sealed class XrayWireguardKeyGeneratorTests
{
    private readonly FakeProcessRunner _processRunner = new();
    private readonly IXrayVersionProvider _versionProvider = Substitute.For<IXrayVersionProvider>();
    private readonly XrayWireguardKeyGenerator _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayWireguardKeyGeneratorTests"/>
    /// </summary>
    public XrayWireguardKeyGeneratorTests()
    {
        _sut = new XrayWireguardKeyGenerator(_processRunner, _versionProvider, Microsoft.Extensions.Options.Options.Create(new XrayOptions { BinaryPath = "/opt/sbin/flowgate-xray" }));
    }

    /// <summary>
    ///     Вывод Xray 26 (как на роутере) разбирается в пару ключей
    /// </summary>
    [Fact]
    public void Parse_Xray26Output_ReturnsKeys()
    {
        var keys = XrayWireguardKeyGenerator.Parse(
            "PrivateKey: aG5QhEoyO8tNJ03jHMXG/wI2Ri5QuvdRvUqWSDEQp1A=\r\nPassword (PublicKey): Kp5mU8LXHf4mVJ2EgkG+K2B9eFdDJzSpOIU6bMgItXo=\r\nHash32: x\r\n");

        Assert.Equal("aG5QhEoyO8tNJ03jHMXG/wI2Ri5QuvdRvUqWSDEQp1A=", keys.PrivateKey);
        Assert.Equal("Kp5mU8LXHf4mVJ2EgkG+K2B9eFdDJzSpOIU6bMgItXo=", keys.PublicKey);
    }

    /// <summary>
    ///     Вывод без ключей — ошибка Xray
    /// </summary>
    [Fact]
    public void Parse_NoKeys_Throws()
    {
        Assert.Throws<XrayOperationException>(() => XrayWireguardKeyGenerator.Parse("unknown command"));
    }

    /// <summary>
    ///     Установленный Xray вызывается как <c>xray wg</c>
    /// </summary>
    [Fact]
    public async Task GenerateAsync_Installed_RunsXrayWg()
    {
        _versionProvider.IsInstalled.Returns(true);

        var keys = await _sut.GenerateAsync(TestContext.Current.CancellationToken);

        Assert.NotEqual(keys.PrivateKey, keys.PublicKey);
        Assert.Equal(["wg"], Assert.Single(_processRunner.Calls).Arguments);
    }

    /// <summary>
    ///     Без установленного Xray — 400 с подсказкой установить его
    /// </summary>
    [Fact]
    public async Task GenerateAsync_NotInstalled_ThrowsValidation()
    {
        _versionProvider.IsInstalled.Returns(false);

        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.GenerateAsync(TestContext.Current.CancellationToken));
        Assert.Empty(_processRunner.Calls);
    }
}
