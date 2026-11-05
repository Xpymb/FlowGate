using FlowGate.Contracts.Maintenance;

namespace FlowGate.Tests.Unit.Maintenance;

/// <summary>
///     Тесты <see cref="AppUpdateCheck"/> и <see cref="ProcStatus"/>
/// </summary>
public sealed class AppUpdateCheckTests
{
    /// <summary>
    ///     Версия разбирается из тега и строки; другие форматы отклоняются
    /// </summary>
    /// <param name="value">Строка</param>
    /// <param name="expected">Ожидаемая версия или null</param>
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("1.10.3", "1.10.3")]
    [InlineData("v1.2", null)]
    [InlineData("1.2.3.4", null)]
    [InlineData("v1.2.3-beta", null)]
    [InlineData(null, null)]
    public void ParseVersion_Formats_ParsesOnlyThreeNumbers(string? value, string? expected)
    {
        Assert.Equal(expected, AppUpdateCheck.ParseVersion(value)?.ToString());
    }

    /// <summary>
    ///     Последняя стабильная версия: черновики, pre-release и чужие теги пропускаются, сравнение числовое
    /// </summary>
    [Fact]
    public void FromReleases_MixedReleases_PicksLatestStable()
    {
        var check = AppUpdateCheck.FromReleases(
            [("v0.9.0", false, false), ("v0.10.0", false, false), ("v0.11.0", false, true), ("v0.12.0", true, false), ("nightly", false, false)],
            DateTimeOffset.UnixEpoch);

        Assert.Equal(new Version(0, 10, 0), check.LatestVersion);
        Assert.True(check.IsNewerThan(new Version(0, 9, 5)));
        Assert.False(check.IsNewerThan(new Version(0, 10, 0)));
        Assert.False(check.IsNewerThan(new Version(1, 0, 0)));
    }

    /// <summary>
    ///     Нет стабильных релизов — обновления нет
    /// </summary>
    [Fact]
    public void FromReleases_NoStable_NoUpdate()
    {
        var check = AppUpdateCheck.FromReleases([("v1.0.0", false, true)], DateTimeOffset.UnixEpoch);

        Assert.Null(check.LatestVersion);
        Assert.False(check.IsNewerThan(new Version(0, 1, 0)));
    }

    /// <summary>
    ///     RSS читается из строки <c>VmRSS</c>; без неё — null
    /// </summary>
    [Fact]
    public void ParseRssKilobytes_StatusFile_ReadsVmRss()
    {
        Assert.Equal(30512, ProcStatus.ParseRssKilobytes("Name:\tflowgate\nVmPeak:\t 99999 kB\nVmRSS:\t   30512 kB\nThreads:\t12\n"));
        Assert.Null(ProcStatus.ParseRssKilobytes("Name:\tflowgate\n"));
        Assert.Null(ProcStatus.ParseRssKilobytes(null));
    }
}
