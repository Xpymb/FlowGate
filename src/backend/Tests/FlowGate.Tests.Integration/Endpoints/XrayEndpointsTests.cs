using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/xray</c> и <c>/api/jobs</c>: установка через API до работающего процесса
/// </summary>
public sealed class XrayEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     По умолчанию в списке только стабильные релизы, первый помечен последним
    /// </summary>
    [Fact]
    public async Task GetReleases_Default_ReturnsStableReleases()
    {
        using var client = _factory.CreateClient();

        var stable = await GetJsonAsync(client, "/api/xray/releases");
        var all = await GetJsonAsync(client, "/api/xray/releases?includePrerelease=true");

        Assert.Equal(["26.10.31", "26.10.21"], stable.EnumerateArray().Select(item => item.GetProperty("version").GetString()));
        Assert.True(stable[0].GetProperty("isLatest").GetBoolean());
        Assert.Equal(3, all.GetArrayLength());
    }

    /// <summary>
    ///     Установка возвращает 202 с задачей, задача завершается, Xray установлен и работает
    /// </summary>
    [Fact]
    public async Task Install_Latest_JobSucceedsAndXrayRuns()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/xray/install", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var job = await ReadJsonAsync(response);
        var jobId = job.GetProperty("id").GetString();
        Assert.Equal($"/api/jobs/{jobId}", response.Headers.Location?.ToString());

        var finished = await WaitForJobAsync(client, jobId!);
        Assert.Equal("Succeeded", finished.GetProperty("status").GetString());

        var status = await GetJsonAsync(client, "/api/xray");
        Assert.True(status.GetProperty("installed").GetBoolean());
        Assert.Equal("26.10.31", status.GetProperty("version").GetString());
        Assert.Equal("Running", status.GetProperty("state").GetString());
        Assert.Equal("26.10.31", FakeXrayBinary.ReadVersion(_factory.XrayBinaryPath));
    }

    /// <summary>
    ///     Неизвестная версия — 404, неверная строка — 400
    /// </summary>
    [Fact]
    public async Task Install_BadVersions_Returns404And400()
    {
        using var client = _factory.CreateClient();

        using var unknown = await client.PostAsJsonAsync("/api/xray/install", new { version = "1.0.0" }, TestContext.Current.CancellationToken);
        using var invalid = await client.PostAsJsonAsync("/api/xray/install", new { version = "newest" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await ReadJsonAsync(invalid)).GetProperty("errorCode").GetString());
    }

    /// <summary>
    ///     Вторая установка во время первой — 409
    /// </summary>
    [Fact]
    public async Task Install_WhileInstalling_Returns409()
    {
        using var gate = new ManualResetEventSlim(false);
        _factory.GitHub.DownloadGate = gate;
        using var client = _factory.CreateClient();

        using var first = await client.PostAsJsonAsync("/api/xray/install", new { }, TestContext.Current.CancellationToken);
        using var second = await client.PostAsJsonAsync("/api/xray/install", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("CONFLICT", (await ReadJsonAsync(second)).GetProperty("errorCode").GetString());

        gate.Set();
        await WaitForJobAsync(client, (await ReadJsonAsync(first)).GetProperty("id").GetString()!);
    }

    /// <summary>
    ///     Стоп и старт меняют состояние, логи процесса видны через API
    /// </summary>
    [Fact]
    public async Task StopStartAndLogs_InstalledXray_Work()
    {
        using var client = _factory.CreateClient();
        using var install = await client.PostAsJsonAsync("/api/xray/install", new { }, TestContext.Current.CancellationToken);
        await WaitForJobAsync(client, (await ReadJsonAsync(install)).GetProperty("id").GetString()!);

        _factory.ProcessLauncher.Started[^1].WriteLine("[Warning] core: Xray 26.10.31 started");
        var logs = await GetJsonAsync(client, "/api/xray/logs?tail=10");
        using var stop = await client.PostAsync("/api/xray/stop", null, TestContext.Current.CancellationToken);
        var stopped = await ReadJsonAsync(stop);
        using var start = await client.PostAsync("/api/xray/start", null, TestContext.Current.CancellationToken);
        var started = await ReadJsonAsync(start);

        Assert.Contains(logs.GetProperty("lines").EnumerateArray(), line => line.GetProperty("text").GetString()!.Contains("started"));
        Assert.Equal("Stopped", stopped.GetProperty("state").GetString());
        Assert.False(stopped.GetProperty("autostart").GetBoolean());
        Assert.Equal("Running", started.GetProperty("state").GetString());
        Assert.True(started.GetProperty("autostart").GetBoolean());
    }

    /// <summary>
    ///     Неизвестная задача — 404
    /// </summary>
    [Fact]
    public async Task GetJob_Unknown_Returns404()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync($"/api/jobs/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static async Task<JsonElement> WaitForJobAsync(HttpClient client, string jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var job = await GetJsonAsync(client, $"/api/jobs/{jobId}");
            if (job.GetProperty("status").GetString() != "Running")
            {
                return job;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Задача {jobId} не завершилась за 10 с");
            }

            await Task.Delay(20);
        }
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        return document.RootElement.Clone();
    }
}
