using FlowGate.Bll.Implementations.Servers;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Тесты <see cref="ServerImportParser"/>: Xray-JSON, base64 и строки
/// </summary>
public class ServerImportParserTests
{
    private readonly ServerImportParser _sut = new();

    /// <summary>
    ///     Из Xray-JSON берутся только серверы; несколько серверов профиля нумеруются; цепочки и неизвестные протоколы отклоняются
    /// </summary>
    [Fact]
    public void Parse_XrayJsonProfiles_ExtractsServersAndRejectsChains()
    {
        var batch = _sut.Parse(SubscriptionFixtures.XrayJsonProfiles);

        Assert.Equal(
            ["🇳🇱 Нидерланды", "🇩🇪 Германия #1", "🇩🇪 Германия #2", "🇩🇪 Германия #3"],
            batch.Servers.Select(server => server.Name));
        Assert.All(batch.Servers, server => Assert.False(server.Outbound.ContainsKey("tag")));
        Assert.Equal(2, batch.Rejected.Count);
        Assert.Contains(batch.Rejected, rejection => rejection.Reason.Contains("Цепочки"));
        Assert.Contains(batch.Rejected, rejection => rejection.Reason.Contains("mtproto"));
        var warning = Assert.Single(batch.Warnings);
        Assert.Contains("routing", warning);
        Assert.Contains("observatory", warning);
    }

    /// <summary>
    ///     Base64 и plain дают одинаковые серверы и метаданные
    /// </summary>
    [Fact]
    public void Parse_Base64AndPlain_GiveSameResult()
    {
        var plain = _sut.Parse(SubscriptionFixtures.PlainList());
        var base64 = _sut.Parse(SubscriptionFixtures.Base64List());

        Assert.Equal(3, plain.Servers.Count);
        Assert.Equal(plain.Servers.Select(server => server.Name), base64.Servers.Select(server => server.Name));
        Assert.Equal("6", base64.BodyMetadata["profile-update-interval"]);
        Assert.Empty(base64.Rejected);
    }

    /// <summary>
    ///     Плохая строка не мешает остальным
    /// </summary>
    [Fact]
    public void Parse_OneBadLink_RejectsOnlyIt()
    {
        var batch = _sut.Parse("vless://id@a.example:443#A\nvless://broken\ntrojan://p@b.example:443#B\nsome note");

        Assert.Equal(["A", "B"], batch.Servers.Select(server => server.Name));
        var rejection = Assert.Single(batch.Rejected);
        Assert.Equal(2, rejection.Index);
        Assert.Contains(batch.Warnings, warning => warning.Contains("без ссылок: 1"));
    }

    /// <summary>
    ///     JSON распознаётся раньше base64, даже если похож на него
    /// </summary>
    [Fact]
    public void Parse_SingleOutboundJson_IsJsonNotBase64()
    {
        var batch = _sut.Parse("""{"tag":"x","protocol":"trojan","settings":{"address":"a.example","port":443,"password":"p"}}""");

        var server = Assert.Single(batch.Servers);
        Assert.Equal("x", server.Name);
        Assert.Equal("trojan", server.Outbound["protocol"]!.GetValue<string>());
    }
}
