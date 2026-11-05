using FlowGate.Contracts.Xray;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Contracts;

/// <summary>
///     Тесты разбора <c>.dgst</c> в <see cref="XrayDigest"/>
/// </summary>
public class XrayDigestTests
{
    /// <summary>
    ///     Из настоящего .dgst берётся строка SHA2-256
    /// </summary>
    [Fact]
    public void Parse_RealDigestFile_ExtractsSha256()
    {
        var digest = XrayDigest.Parse(GitHubFixtures.XrayDigest);

        Assert.Equal("1811c8f1da0c3b6dc707ebaf1c8390e987ba39859a8f0a7aadd8526157f2ec8d", digest.Sha256Hex);
    }

    /// <summary>
    ///     Совпадение и несовпадение хеша
    /// </summary>
    [Fact]
    public void Matches_SameAndOtherHash_ReturnsTrueOnlyForSame()
    {
        var digest = XrayDigest.Parse(GitHubFixtures.XrayDigest);
        var same = Convert.FromHexString("1811c8f1da0c3b6dc707ebaf1c8390e987ba39859a8f0a7aadd8526157f2ec8d");
        var other = new byte[32];

        Assert.True(digest.Matches(same));
        Assert.False(digest.Matches(other));
    }

    /// <summary>
    ///     Файл без SHA2-256 или с битым хешем отклоняется
    /// </summary>
    [Theory]
    [InlineData("MD5= cec5d97aedf12fce6121c29822e19c41")]
    [InlineData("SHA2-256= 1234")]
    [InlineData("SHA2-256= zz11c8f1da0c3b6dc707ebaf1c8390e987ba39859a8f0a7aadd8526157f2ec8d")]
    public void Parse_InvalidContent_ThrowsFormatException(string content)
    {
        Assert.Throws<FormatException>(() => XrayDigest.Parse(content));
    }
}
