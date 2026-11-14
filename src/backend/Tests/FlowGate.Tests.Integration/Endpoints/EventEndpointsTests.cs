using System.Net;
using System.Net.Http.Json;

using Microsoft.Extensions.DependencyInjection;

using FlowGate.Shared.Changes;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>GET /api/events</c> (Server-Sent Events)
/// </summary>
public sealed class EventEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Поток сразу отдаёт ping, а затем change с темой опубликованного изменения
    /// </summary>
    [Fact]
    public async Task GetEvents_ChangePublished_StreamsPingThenChange()
    {
        using var client = _factory.CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no", Assert.Single(response.Headers.GetValues("X-Accel-Buffering")));

        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        Assert.Equal("ping", (await ReadEventAsync(reader, timeout.Token)).Type);

        _factory.Services.GetRequiredService<IChangeFeed>().Publish("servers");

        var change = await ReadEventAsync(reader, timeout.Token);
        Assert.Equal(("change", "servers"), change);
    }

    /// <summary>
    ///     Запись данных через API публикует тему её файла
    /// </summary>
    [Fact]
    public async Task GetEvents_SettingsSaved_StreamsSettingsChange()
    {
        using var client = _factory.CreateClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        using var response = await client.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var reader = new StreamReader(stream);
        await ReadEventAsync(reader, timeout.Token);

        using var save = await client.PutAsJsonAsync(
            "/api/settings",
            new { xrayLogLevel = "warning", updateProxy = new { enabled = false }, intervals = new { } },
            timeout.Token);
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var change = await ReadEventAsync(reader, timeout.Token);
        while (change.Data != "settings")
        {
            change = await ReadEventAsync(reader, timeout.Token);
        }

        Assert.Equal(("change", "settings"), change);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static async Task<(string Type, string Data)> ReadEventAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        var type = "message";
        var data = string.Empty;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                return (type, data);
            }

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                type = line["event: ".Length..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line["data: ".Length..];
            }
        }

        throw new EndOfStreamException("Поток событий закрылся");
    }
}
