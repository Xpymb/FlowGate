using System.Net;

using FlowGate.Exceptions;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Unit.GitHub;

/// <summary>
///     Тесты <see cref="GitHubReleasesClient"/> на настоящем ответе GitHub
/// </summary>
public sealed class GitHubReleasesClientTests : IDisposable
{
    private static readonly Uri ApiBaseAddress = new("https://api.github.com/");

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Релизы разбираются из snake_case, запрос идёт на /repos/{repo}/releases?per_page=N
    /// </summary>
    [Fact]
    public async Task GetReleasesAsync_RealResponse_ParsesReleasesAndAssets()
    {
        var handler = FakeHttpMessageHandler.RespondJson(GitHubFixtures.XrayReleases);
        var sut = CreateClient(handler);

        var releases = await sut.GetReleasesAsync("XTLS/Xray-core", 3, TestContext.Current.CancellationToken);

        Assert.Equal(new Uri("https://api.github.com/repos/XTLS/Xray-core/releases?per_page=3"), Assert.Single(handler.Requests).Uri);
        Assert.Equal(["v26.10.31", "v26.10.21", "v26.9.30"], releases.Select(release => release.TagName));
        Assert.Equal([false, false, true], releases.Select(release => release.Prerelease));
        var asset = releases[0].Assets.Single(item => item.Name == "Xray-linux-arm64-v8a.zip");
        Assert.True(asset.Size > 1_000_000);
        Assert.EndsWith("/v26.10.31/Xray-linux-arm64-v8a.zip", asset.BrowserDownloadUrl);
        Assert.NotNull(releases[0].PublishedAt);
    }

    /// <summary>
    ///     Исчерпанный лимит API даёт понятное сообщение
    /// </summary>
    [Fact]
    public async Task GetReleasesAsync_RateLimited_ThrowsWithLimitMessage()
    {
        var handler = new FakeHttpMessageHandler(_ =>
        {
            var response = FakeHttpMessageHandler.Json("{}", HttpStatusCode.Forbidden);
            response.Headers.Add("X-RateLimit-Remaining", "0");
            return response;
        });
        var sut = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => sut.GetReleasesAsync("XTLS/Xray-core", 3, TestContext.Current.CancellationToken));

        Assert.Contains("лимит", exception.Message);
    }

    /// <summary>
    ///     Сеть недоступна — ExternalServiceUnavailableException
    /// </summary>
    [Fact]
    public async Task GetReleasesAsync_NetworkError_ThrowsExternalUnavailable()
    {
        var sut = CreateClient(FakeHttpMessageHandler.Throw(new HttpRequestException("Name or service not known")));

        await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => sut.GetReleasesAsync("XTLS/Xray-core", 3, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Файл скачивается целиком, возвращается размер
    /// </summary>
    [Fact]
    public async Task DownloadAsync_SmallFile_WritesFileAndReturnsSize()
    {
        var content = new byte[4096];
        Random.Shared.NextBytes(content);
        var sut = CreateClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Bytes(content)));
        Directory.CreateDirectory(_directory);
        var destination = Path.Combine(_directory, "file.bin");

        var size = await sut.DownloadAsync(new Uri("https://example.com/file.bin"), destination, 1_000_000, TestContext.Current.CancellationToken);

        Assert.Equal(content.Length, size);
        Assert.Equal(content, await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Файл больше предела отклоняется
    /// </summary>
    [Fact]
    public async Task DownloadAsync_FileExceedsLimit_ThrowsExternalUnavailable()
    {
        var sut = CreateClient(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Bytes(new byte[2048])));
        Directory.CreateDirectory(_directory);

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => sut.DownloadAsync(
            new Uri("https://example.com/big.zip"),
            Path.Combine(_directory, "big.zip"),
            1024,
            TestContext.Current.CancellationToken));

        Assert.Contains("big.zip", exception.Message);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static GitHubReleasesClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = ApiBaseAddress },
            Microsoft.Extensions.Options.Options.Create(new GitHubOptions()));
}
