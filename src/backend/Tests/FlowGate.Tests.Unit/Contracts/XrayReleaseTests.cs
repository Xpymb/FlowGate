using FlowGate.Contracts.Xray;

namespace FlowGate.Tests.Unit.Contracts;

/// <summary>
///     Тесты отбора релизов в <see cref="XrayRelease.TryCreate"/>
/// </summary>
public class XrayReleaseTests
{
    private const string Archive = "Xray-linux-arm64-v8a.zip";

    private static readonly (string Name, string Url, long Size)[] FullAssets =
    [
        ("Xray-linux-64.zip", "https://example.com/x64.zip", 10),
        (Archive, "https://example.com/arm64.zip", 13_000_000),
        (Archive + ".dgst", "https://example.com/arm64.zip.dgst", 300),
    ];

    /// <summary>
    ///     Релиз с архивом и .dgst создаётся
    /// </summary>
    [Fact]
    public void TryCreate_ArchiveAndDigestPresent_ReturnsRelease()
    {
        var release = XrayRelease.TryCreate("v26.10.31", DateTimeOffset.UnixEpoch, false, false, FullAssets, Archive);

        Assert.NotNull(release);
        Assert.Equal("26.10.31", release.Version.ToString());
        Assert.Equal(new Uri("https://example.com/arm64.zip"), release.ArchiveUrl);
        Assert.Equal(13_000_000, release.ArchiveSize);
    }

    /// <summary>
    ///     Черновик, релиз без .dgst и релиз с нечисловым тегом отбрасываются
    /// </summary>
    [Fact]
    public void TryCreate_UnsuitableReleases_ReturnsNull()
    {
        Assert.Null(XrayRelease.TryCreate("v26.10.31", null, false, true, FullAssets, Archive));
        Assert.Null(XrayRelease.TryCreate("v26.10.31", null, false, false, FullAssets.Take(2).ToList(), Archive));
        Assert.Null(XrayRelease.TryCreate("nightly", null, false, false, FullAssets, Archive));
    }
}
