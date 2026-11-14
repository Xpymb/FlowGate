using System.Net;
using System.Net.Http.Json;

using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/subscriptions</c>: добавление, обновление, ротация учётных данных, удаление
/// </summary>
public sealed class SubscriptionEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Добавление подписки загружает серверы, повторное обновление со сменой пароля сохраняет ID, удаление убирает серверы
    /// </summary>
    [Fact]
    public async Task Lifecycle_CreateRefreshRotateDelete_Works()
    {
        using var client = _factory.CreateClient();

        var created = await ApiJson.PostAsync(client, "/api/subscriptions", new { url = "https://sub.example.com/sub/token" }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString();
        await ApiJson.WaitForJobAsync(client, created.GetProperty("refreshJobId").GetString()!);
        var subscription = await ApiJson.GetAsync(client, $"/api/subscriptions/{id}");
        var firstIds = (await ApiJson.GetAsync(client, "/api/servers")).EnumerateArray().Select(server => server.GetProperty("id").GetString()).ToList();

        _factory.SubscriptionHttp.Responder = _ => FakeHttpMessageHandler.Json(SubscriptionFixtures.Base64List("rotated"));
        var job = await ApiJson.PostAsync(client, $"/api/subscriptions/{id}/refresh", new { }, HttpStatusCode.Accepted);
        var finished = await ApiJson.WaitForJobAsync(client, job.GetProperty("id").GetString()!);
        var secondIds = (await ApiJson.GetAsync(client, "/api/servers")).EnumerateArray().Select(server => server.GetProperty("id").GetString()).ToList();
        var refreshed = await ApiJson.GetAsync(client, $"/api/subscriptions/{id}");

        using var delete = await client.DeleteAsync($"/api/subscriptions/{id}", TestContext.Current.CancellationToken);
        var after = await ApiJson.GetAsync(client, "/api/servers");

        Assert.Equal(3, subscription.GetProperty("serverCount").GetInt32());
        Assert.Equal("Мой VPN", subscription.GetProperty("displayName").GetString());
        Assert.Equal("Ok", subscription.GetProperty("lastRefresh").GetProperty("status").GetString());
        Assert.Equal("Succeeded", finished.GetProperty("status").GetString());
        Assert.Equal(firstIds, secondIds);
        Assert.Equal(1, refreshed.GetProperty("lastRefresh").GetProperty("updated").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, after.GetArrayLength());
        var request = _factory.SubscriptionHttp.Requests[0];
        Assert.Equal("https://sub.example.com/sub/token", request.Uri!.ToString());
    }

    /// <summary>
    ///     Предпросмотр подписки показывает серверы с ключами, по которым мастер находит их после создания подписки
    /// </summary>
    [Fact]
    public async Task Preview_ThenCreate_ImportKeysMatchAndPreviewSavesNothing()
    {
        using var client = _factory.CreateClient();

        var preview = await ApiJson.PostAsync(client, "/api/subscriptions/preview", new { url = "https://sub.example.com/sub/token" }, HttpStatusCode.OK);
        var before = await ApiJson.GetAsync(client, "/api/subscriptions");
        var created = await ApiJson.PostAsync(client, "/api/subscriptions", new { url = "https://sub.example.com/sub/token" }, HttpStatusCode.Created);
        await ApiJson.WaitForJobAsync(client, created.GetProperty("refreshJobId").GetString()!);
        var servers = await ApiJson.GetAsync(client, "/api/servers");

        var previewKeys = preview.GetProperty("servers").EnumerateArray().Select(server => server.GetProperty("importKey").GetString()).Order().ToList();
        var storedKeys = servers.EnumerateArray().Select(server => server.GetProperty("importKey").GetString()).Order().ToList();
        Assert.Equal("Мой VPN", preview.GetProperty("title").GetString());
        Assert.Equal(0, before.GetArrayLength());
        Assert.Equal(3, previewKeys.Count);
        Assert.Equal(previewKeys, storedKeys);
    }

    /// <summary>
    ///     Сервер подписки нельзя править вручную
    /// </summary>
    [Fact]
    public async Task UpdateServer_FromSubscription_Returns400()
    {
        using var client = _factory.CreateClient();
        var created = await ApiJson.PostAsync(client, "/api/subscriptions", new { url = "https://sub.example.com/sub/token" }, HttpStatusCode.Created);
        await ApiJson.WaitForJobAsync(client, created.GetProperty("refreshJobId").GetString()!);
        var server = (await ApiJson.GetAsync(client, "/api/servers"))[0];
        var details = await ApiJson.GetAsync(client, $"/api/servers/{server.GetProperty("id").GetString()}");

        using var response = await client.PutAsJsonAsync(
            $"/api/servers/{server.GetProperty("id").GetString()}",
            new { name = "X", outbound = details.GetProperty("outbound") },
            TestContext.Current.CancellationToken);

        Assert.False(details.GetProperty("editable").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    ///     Ошибка провайдера видна в подписке и в задаче
    /// </summary>
    [Fact]
    public async Task Refresh_ProviderError_RecordedInSubscription()
    {
        _factory.SubscriptionHttp.Responder = _ => FakeHttpMessageHandler.Json("down", HttpStatusCode.BadGateway);
        using var client = _factory.CreateClient();

        var created = await ApiJson.PostAsync(client, "/api/subscriptions", new { url = "https://sub.example.com/sub/token" }, HttpStatusCode.Created);
        var job = await ApiJson.WaitForJobAsync(client, created.GetProperty("refreshJobId").GetString()!);
        var subscription = await ApiJson.GetAsync(client, $"/api/subscriptions/{created.GetProperty("id").GetString()}");

        Assert.Equal("Failed", job.GetProperty("status").GetString());
        Assert.Equal("Failed", subscription.GetProperty("lastRefresh").GetProperty("status").GetString());
        Assert.Contains("502", subscription.GetProperty("lastRefresh").GetProperty("error").GetString());
        Assert.DoesNotContain("token", subscription.GetProperty("lastRefresh").GetProperty("error").GetString());
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
