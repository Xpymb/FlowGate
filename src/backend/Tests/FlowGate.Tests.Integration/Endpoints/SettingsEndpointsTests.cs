using System.Net;
using System.Net.Http.Json;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/settings</c>
/// </summary>
public sealed class SettingsEndpointsTests : IDisposable
{
    private const string Link = "trojan://p@de.example.net:443?sni=de.example.net#DE";

    private readonly FlowGateWebApplicationFactory _factory = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Без сохранённых настроек: прокси выключен, интервалы из конфигурации, состояние прокси — Disabled
    /// </summary>
    [Fact]
    public async Task GetSettings_Defaults_ProxyDisabled()
    {
        using var client = _factory.CreateClient();

        var settings = await ApiJson.GetAsync(client, "/api/settings");
        var status = await ApiJson.GetAsync(client, "/api/settings/update-proxy/status");

        Assert.Equal("warning", settings.GetProperty("xrayLogLevel").GetString());
        Assert.False(settings.GetProperty("updateProxy").GetProperty("enabled").GetBoolean());
        Assert.Equal(12, settings.GetProperty("intervals").GetProperty("subscriptionDefault").GetProperty("effectiveHours").GetInt32());
        Assert.Equal("Disabled", status.GetProperty("state").GetString());
    }

    /// <summary>
    ///     Сохранение с сервером: настройки читаются обратно, сервер прокси удалить нельзя
    /// </summary>
    [Fact]
    public async Task PutSettings_WithServer_SavedAndServerProtected()
    {
        using var client = _factory.CreateClient();
        var draft = await ApiJson.PostAsync(client, "/api/servers/parse", new { text = Link }, HttpStatusCode.OK);
        var created = await ApiJson.PostAsync(
            client,
            "/api/servers",
            new { name = "DE", outbound = draft.GetProperty("outbound") },
            HttpStatusCode.Created);
        var serverId = created.GetProperty("server").GetProperty("id").GetString();

        using var response = await client.PutAsJsonAsync(
            "/api/settings",
            new
            {
                xrayLogLevel = "info",
                updateProxy = new { enabled = true, serverId, extraDomains = new[] { "example.com" } },
                intervals = new { xrayUpdateCheckHours = 6 },
            },
            Token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var settings = await ApiJson.GetAsync(client, "/api/settings");
        Assert.Equal("info", settings.GetProperty("xrayLogLevel").GetString());
        Assert.Equal(serverId, settings.GetProperty("updateProxy").GetProperty("serverId").GetString());
        Assert.Equal("domain:example.com", settings.GetProperty("updateProxy").GetProperty("extraDomains")[0].GetString());
        Assert.Equal(6, settings.GetProperty("intervals").GetProperty("xrayUpdateCheck").GetProperty("effectiveHours").GetInt32());

        await ApiJson.DeleteAsync(client, $"/api/servers/{serverId}", HttpStatusCode.Conflict);
    }

    /// <summary>
    ///     Включение без сервера и неизвестный уровень логов — 400 со списком ошибок
    /// </summary>
    [Fact]
    public async Task PutSettings_Invalid_BadRequest()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PutAsJsonAsync(
            "/api/settings",
            new { xrayLogLevel = "loud", updateProxy = new { enabled = true }, intervals = new { } },
            Token);
        var error = await ApiJson.ReadAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("errorCode").GetString());
        Assert.False((await ApiJson.GetAsync(client, "/api/settings")).GetProperty("updateProxy").GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    ///     Путь загрузки принимается строкой (Proxy/Direct), неизвестное значение — 400 до вызова сервиса
    /// </summary>
    [Fact]
    public async Task PostRefresh_Via_ParsedAsStringEnum()
    {
        using var client = _factory.CreateClient();

        using var known = await client.PostAsync("/api/subscriptions/missing/refresh?via=Direct", content: null, Token);
        using var unknown = await client.PostAsync("/api/domain-sources/missing/refresh?via=Bogus", content: null, Token);

        Assert.Equal(HttpStatusCode.NotFound, known.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
