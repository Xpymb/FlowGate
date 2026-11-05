using System.Net;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/system</c>
/// </summary>
public sealed class SystemEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Состояние: версия есть, обновление не проверялось, FlowGate запущен не из пакета
    /// </summary>
    [Fact]
    public async Task GetSystem_NotPackaged_ReturnsVersionAndNotInstalled()
    {
        using var client = _factory.CreateClient();

        var status = await ApiJson.GetAsync(client, "/api/system");

        Assert.False(string.IsNullOrEmpty(status.GetProperty("version").GetString()));
        Assert.False(status.GetProperty("packageInstalled").GetBoolean());
        Assert.False(status.GetProperty("updateAvailable").GetBoolean());
        Assert.True(status.GetProperty("uptimeSeconds").GetInt64() >= 0);
    }

    /// <summary>
    ///     Самообновление без пакета — 409
    /// </summary>
    [Fact]
    public async Task PostUpdate_NotPackaged_Conflict()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync("/api/system/update", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.DoesNotContain(_factory.ProcessRunner.Calls, call => call.FileName.EndsWith("start-stop-daemon", StringComparison.Ordinal));
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
