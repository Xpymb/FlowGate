using FlowGate.Contracts.Connections;

namespace FlowGate.Tests.Unit.Connections;

/// <summary>
///     Тесты <see cref="ConnectionAllocator"/>: свободные порты, имена интерфейсов и адреса WireGuard
/// </summary>
public sealed class ConnectionAllocatorTests
{
    /// <summary>
    ///     Берётся наименьший свободный порт, включая дыры после удалённых соединений
    /// </summary>
    [Fact]
    public void NextPort_WithGap_ReturnsLowestFree()
    {
        Assert.Equal(10801, ConnectionAllocator.NextPort([10800, 10802], 10800, 10805));
    }

    /// <summary>
    ///     Занятый целиком диапазон даёт null
    /// </summary>
    [Fact]
    public void NextPort_RangeExhausted_ReturnsNull()
    {
        Assert.Null(ConnectionAllocator.NextPort([10800, 10801], 10800, 10801));
    }

    /// <summary>
    ///     Имена интерфейсов, существующие в Keenetic, пропускаются без учёта регистра
    /// </summary>
    [Fact]
    public void NextInterfaceName_SkipsExistingIgnoringCase()
    {
        Assert.Equal("Proxy2", ConnectionAllocator.NextInterfaceName("Proxy", ["proxy0", "Proxy1", "Wireguard0"]));
    }

    /// <summary>
    ///     Адреса WireGuard начинаются с .2 и не повторяются
    /// </summary>
    [Fact]
    public void NextWireguardAddress_SkipsUsed()
    {
        Assert.Equal("10.255.0.4", ConnectionAllocator.NextWireguardAddress("10.255.0", ["10.255.0.2", "10.255.0.3"]));
    }

    /// <summary>
    ///     Занятая подсеть даёт null
    /// </summary>
    [Fact]
    public void NextWireguardAddress_SubnetExhausted_ReturnsNull()
    {
        var used = Enumerable.Range(2, 253).Select(host => $"10.255.0.{host}");

        Assert.Null(ConnectionAllocator.NextWireguardAddress("10.255.0", used));
    }
}
