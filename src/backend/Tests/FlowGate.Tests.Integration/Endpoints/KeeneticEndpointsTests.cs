using System.Net;
using System.Text.Json;

using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>GET /api/keenetic/system</c> и формата ошибок
/// </summary>
public sealed class KeeneticEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Сведения о роутере берутся из RCI show/version
    /// </summary>
    [Fact]
    public async Task GetSystemInfo_RciAvailable_ReturnsRouterInfo()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/keenetic/system", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("Hopper SE (KN-3812)", json.RootElement.GetProperty("model").GetString());
        Assert.True(json.RootElement.GetProperty("isSupported").GetBoolean());
        var request = Assert.Single(_factory.Rci.Requests);
        Assert.Equal(new Uri("http://127.0.0.1:79/rci/show/version"), request.Uri);
    }

    /// <summary>
    ///     Недоступный RCI даёт 503 с кодом ошибки и traceId
    /// </summary>
    [Fact]
    public async Task GetSystemInfo_RciUnavailable_Returns503WithErrorBody()
    {
        _factory.Rci.Responder = _ => throw new HttpRequestException("Connection refused");
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/keenetic/system", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("KEENETIC_UNAVAILABLE", json.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("message").GetString()));
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("traceId").GetString()));
    }

    /// <summary>
    ///     HTTP-ошибка RCI тоже даёт 503
    /// </summary>
    [Fact]
    public async Task GetSystemInfo_RciReturns500_Returns503()
    {
        _factory.Rci.Responder = _ => FakeHttpMessageHandler.Json("{}", HttpStatusCode.InternalServerError);
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/keenetic/system", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
