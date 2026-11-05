using FlowGate.Contracts.UpdateProxy;

namespace FlowGate.Tests.Unit.UpdateProxy;

/// <summary>
///     Тесты <see cref="UpdateProxyDomains"/>: приведение доменов пользователя и сбор полного списка
/// </summary>
public sealed class UpdateProxyDomainsTests
{
    /// <summary>
    ///     Голый хост становится <c>domain:</c>, префиксы сохраняются, регистр и точка в конце убираются
    /// </summary>
    /// <param name="input">Строка пользователя</param>
    /// <param name="expected">Домен в формате Xray</param>
    [Theory]
    [InlineData("Example.COM", "domain:example.com")]
    [InlineData("  domain:example.com.  ", "domain:example.com")]
    [InlineData("full:cdn.example.com", "full:cdn.example.com")]
    [InlineData("xn--80ak6aa92e.com", "domain:xn--80ak6aa92e.com")]
    public void Normalize_ValidDomain_ReturnsXrayFormat(string input, string expected)
    {
        Assert.Equal(expected, UpdateProxyDomains.Normalize(input));
    }

    /// <summary>
    ///     Ссылка, путь, порт, пробелы и пустая строка — не домен
    /// </summary>
    /// <param name="input">Строка пользователя</param>
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("example.com:443")]
    [InlineData("exa mple.com")]
    [InlineData("regexp:.*")]
    [InlineData("-bad.com")]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_NotDomain_ReturnsNull(string input)
    {
        Assert.Null(UpdateProxyDomains.Normalize(input));
    }

    /// <summary>
    ///     В список попадают встроенные домены, хосты ссылок без пути и токена, свои домены; повторы убираются
    /// </summary>
    [Fact]
    public void Collect_UrlsAndExtra_BuiltInFirstHostsOnlyWithoutDuplicates()
    {
        var domains = UpdateProxyDomains.Collect(
            ["https://sub.example.com/api/secret-token?x=1", "https://SUB.example.com/other", "http://10.0.0.1/list", "not a url"],
            ["domain:github.com", "full:extra.example.org", "full:sub.example.com"]);

        Assert.Equal(
            [.. UpdateProxyDomains.BuiltIn, "full:sub.example.com", "full:extra.example.org"],
            domains);
        Assert.DoesNotContain(domains, domain => domain.Contains("secret", StringComparison.Ordinal));
    }
}
