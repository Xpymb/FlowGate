using System.Net;
using System.Text.Json;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>GET /api/health</c> и OpenAPI
/// </summary>
public sealed class HealthEndpointsTests : IDisposable
{
    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Health отвечает ok и версией без обращения к Keenetic
    /// </summary>
    [Fact]
    public async Task GetHealth_AppRunning_ReturnsOkAndVersion()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ok", json.RootElement.GetProperty("status").GetString());
        Assert.False(string.IsNullOrEmpty(json.RootElement.GetProperty("version").GetString()));
        Assert.Empty(_factory.Rci.Requests);
    }

    /// <summary>
    ///     OpenAPI-документ описывает эндпоинты API
    /// </summary>
    [Fact]
    public async Task GetOpenApi_AppRunning_DescribesApiEndpoints()
    {
        using var client = _factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Contains("/api/health", document);
        Assert.Contains("/api/keenetic/system", document);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
