using System.Text;

using FlowGate.Bll.Implementations.Subscriptions;

namespace FlowGate.Tests.Unit.Subscriptions;

/// <summary>
///     Тесты <see cref="ProviderMetadataParser"/>
/// </summary>
public class ProviderMetadataParserTests
{
    /// <summary>
    ///     Заголовки разбираются, base64-значения декодируются, userinfo раскладывается по полям
    /// </summary>
    [Fact]
    public void Parse_Headers_ReadsAllFields()
    {
        var headers = Headers(
            ("profile-title", "base64:" + Convert.ToBase64String(Encoding.UTF8.GetBytes("Мой VPN"))),
            ("subscription-userinfo", "upload=10; download=20; total=1000; expire=1893456000"),
            ("profile-update-interval", "12"),
            ("announce", "Плановые работы"),
            ("support-url", "https://t.me/support"),
            ("profile-web-page-url", "javascript:alert(1)"));

        var metadata = ProviderMetadataParser.Parse(headers, Headers());

        Assert.NotNull(metadata);
        Assert.Equal("Мой VPN", metadata.Title);
        Assert.Equal(10, metadata.UploadBytes);
        Assert.Equal(20, metadata.DownloadBytes);
        Assert.Equal(1000, metadata.TotalBytes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1893456000), metadata.ExpireAt);
        Assert.Equal(12, metadata.UpdateIntervalHours);
        Assert.Equal("https://t.me/support", metadata.SupportUrl);
        Assert.Null(metadata.WebPageUrl);
    }

    /// <summary>
    ///     Значение из тела важнее заголовка
    /// </summary>
    [Fact]
    public void Parse_BodyAndHeaders_BodyWins()
    {
        var metadata = ProviderMetadataParser.Parse(
            Headers(("profile-title", "Из заголовка"), ("profile-update-interval", "24")),
            Headers(("profile-title", "Из тела")));

        Assert.Equal("Из тела", metadata!.Title);
        Assert.Equal(24, metadata.UpdateIntervalHours);
    }

    /// <summary>
    ///     Нули и мусор не превращаются в значения
    /// </summary>
    [Fact]
    public void Parse_ZerosAndGarbage_AreNull()
    {
        var metadata = ProviderMetadataParser.Parse(
            Headers(("subscription-userinfo", "upload=abc; download=5; total=0; expire=0"), ("profile-update-interval", "999")),
            Headers());

        Assert.NotNull(metadata);
        Assert.Null(metadata.UploadBytes);
        Assert.Equal(5, metadata.DownloadBytes);
        Assert.Null(metadata.TotalBytes);
        Assert.Null(metadata.ExpireAt);
        Assert.Null(metadata.UpdateIntervalHours);
    }

    /// <summary>
    ///     Пустые значения, как у реального провайдера (<c>announce: base64:</c>, пустой <c>announce-url</c>), не становятся текстом
    /// </summary>
    [Fact]
    public void Parse_EmptyBase64AndEmptyValues_AreNull()
    {
        var metadata = ProviderMetadataParser.Parse(
            Headers(("announce", "base64:"), ("announce-url", ""), ("profile-title", "base64:QW5kcmV5J3MgcHJveHk=")),
            Headers());

        Assert.Equal("Andrey's proxy", metadata!.Title);
        Assert.Null(metadata.Announce);
        Assert.Null(metadata.AnnounceUrl);
    }

    /// <summary>
    ///     Без метаданных — null
    /// </summary>
    [Fact]
    public void Parse_Nothing_ReturnsNull()
    {
        Assert.Null(ProviderMetadataParser.Parse(Headers(("content-type", "text/plain")), Headers()));
    }

    private static Dictionary<string, string> Headers(params (string Key, string Value)[] values) =>
        values.ToDictionary(value => value.Key, value => value.Value, StringComparer.OrdinalIgnoreCase);
}
