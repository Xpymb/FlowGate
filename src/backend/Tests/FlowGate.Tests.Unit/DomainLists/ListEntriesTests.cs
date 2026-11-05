using FlowGate.Contracts.DomainLists;

namespace FlowGate.Tests.Unit.DomainLists;

/// <summary>
///     Тесты <see cref="ListEntries"/>: нормализация доменов, адресов и подсетей
/// </summary>
public sealed class ListEntriesTests
{
    /// <summary>
    ///     Значения приводятся к одной записи, чтобы не было дубликатов в Keenetic
    /// </summary>
    /// <param name="value">Исходное значение</param>
    /// <param name="expected">Нормализованное</param>
    /// <param name="kind">Ожидаемый вид</param>
    [Theory]
    [InlineData("WWW.Example.COM.", "www.example.com", ListEntryKind.Domain)]
    [InlineData("_dmarc.example.com", "_dmarc.example.com", ListEntryKind.Domain)]
    [InlineData("149.154.167.51", "149.154.167.51", ListEntryKind.Ipv4)]
    [InlineData("91.108.56.1/22", "91.108.56.0/22", ListEntryKind.Ipv4)]
    [InlineData("10.0.0.7/32", "10.0.0.7", ListEntryKind.Ipv4)]
    [InlineData("2001:067c:04e8:0000::/48", "2001:67c:4e8::/48", ListEntryKind.Ipv6)]
    [InlineData("2001:b28:f23d::a/128", "2001:b28:f23d::a", ListEntryKind.Ipv6)]
    public void TryNormalize_Valid_ReturnsCanonical(string value, string expected, ListEntryKind kind)
    {
        Assert.True(ListEntries.TryNormalize(value, out var entry, out var actualKind));

        Assert.Equal(expected, entry);
        Assert.Equal(kind, actualKind);
        Assert.Equal(kind, ListEntries.KindOf(entry));
    }

    /// <summary>
    ///     Некорректные значения отклоняются: кириллица, пробелы, дефис по краю, неверный префикс
    /// </summary>
    /// <param name="value">Значение</param>
    [Theory]
    [InlineData("пример.рф")]
    [InlineData("exa mple.com")]
    [InlineData("-bad.com")]
    [InlineData("a..b")]
    [InlineData("10.0.0.0/33")]
    [InlineData("::1/129")]
    [InlineData("")]
    public void TryNormalize_Invalid_ReturnsFalse(string value)
    {
        Assert.False(ListEntries.TryNormalize(value, out _, out _));
    }

    /// <summary>
    ///     Адрес из geoip.dat с битами хоста нормализуется
    /// </summary>
    [Fact]
    public void TryFromNetwork_HostBits_Cleared()
    {
        Assert.True(ListEntries.TryFromNetwork([91, 108, 4, 77], 22, out var entry));

        Assert.Equal("91.108.4.0/22", entry);
    }
}
