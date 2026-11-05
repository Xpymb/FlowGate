using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/connections</c>: сервер → соединение → интерфейс Keenetic и inbound Xray → удаление
/// </summary>
public sealed class ConnectionEndpointsTests : IDisposable
{
    private const string TrojanLink = "trojan://p@de.example.net:443?sni=de.example.net#DE";

    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Полный цикл socks-соединения: создание, состояние, защита сервера, переименование, удаление
    /// </summary>
    [Fact]
    public async Task SocksLifecycle_CreateListRenameDelete_Works()
    {
        using var client = _factory.CreateClient();
        var serverId = await CreateServerAsync(client);

        var created = await ApiJson.PostAsync(client, "/api/connections", new { name = "YouTube", kind = "Socks", serverId }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString();
        var config = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(_factory.DataDirectory, "xray.json"), TestContext.Current.CancellationToken))!;
        var list = await ApiJson.GetAsync(client, "/api/connections");
        var deleteServer = await ApiJson.DeleteAsync(client, $"/api/servers/{serverId}", HttpStatusCode.Conflict);
        using var rename = await client.PutAsJsonAsync($"/api/connections/{id}", new { name = "Видео", serverId }, TestContext.Current.CancellationToken);
        using var delete = await client.DeleteAsync($"/api/connections/{id}", TestContext.Current.CancellationToken);

        Assert.Equal("Proxy0", created.GetProperty("interfaceName").GetString());
        Assert.Equal("up", created.GetProperty("interface").GetProperty("state").GetString());
        Assert.Equal($"in-{id}", config["inbounds"]![0]!["tag"]!.GetValue<string>());
        Assert.Equal($"out-{id}", config["routing"]!["rules"]![0]!["outboundTag"]!.GetValue<string>());
        Assert.Equal(1, list.GetArrayLength());
        Assert.Equal("CONFLICT", deleteServer.GetProperty("errorCode").GetString());
        Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
        Assert.Contains("interface Proxy0 description \"FlowGate: Видео\"", _factory.Keenetic.Commands);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Empty(_factory.Keenetic.InterfaceNames);
    }

    /// <summary>
    ///     WireGuard-соединение с установленным Xray: ключи из <c>xray wg</c>, интерфейс Wireguard0
    /// </summary>
    [Fact]
    public async Task Create_WireguardWithXrayInstalled_CreatesWireguardInterface()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_factory.XrayBinaryPath)!);
        await File.WriteAllTextAsync(_factory.XrayBinaryPath, FakeXrayBinary.Content("26.10.31"), TestContext.Current.CancellationToken);
        using var client = _factory.CreateClient();
        var serverId = await CreateServerAsync(client);

        var created = await ApiJson.PostAsync(client, "/api/connections", new { name = "Игры", kind = "Wireguard", serverId }, HttpStatusCode.Created);

        Assert.Equal("Wireguard0", created.GetProperty("interfaceName").GetString());
        Assert.True(created.GetProperty("supportsUdp").GetBoolean());
        Assert.Contains(_factory.ProcessRunner.Calls, call => call.Arguments is ["wg"]);
        Assert.Contains(_factory.Keenetic.Commands, command => command.StartsWith("interface Wireguard0 wireguard peer ", StringComparison.Ordinal));
    }

    /// <summary>
    ///     WireGuard без установленного Xray — 400, Keenetic не трогается
    /// </summary>
    [Fact]
    public async Task Create_WireguardWithoutXray_Returns400()
    {
        using var client = _factory.CreateClient();
        var serverId = await CreateServerAsync(client);

        var error = await ApiJson.PostAsync(client, "/api/connections", new { name = "Игры", kind = "Wireguard", serverId }, HttpStatusCode.BadRequest);

        Assert.Equal("VALIDATION_ERROR", error.GetProperty("errorCode").GetString());
        Assert.Empty(_factory.Keenetic.Commands);
    }

    /// <summary>
    ///     Неизвестное соединение — 404
    /// </summary>
    [Fact]
    public async Task Get_Unknown_Returns404()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/connections/con-missing", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static async Task<string> CreateServerAsync(HttpClient client)
    {
        var draft = await ApiJson.PostAsync(client, "/api/servers/parse", new { text = TrojanLink }, HttpStatusCode.OK);
        var outbound = JsonNode.Parse(draft.GetProperty("outbound").GetRawText());
        var created = await ApiJson.PostAsync(client, "/api/servers", new { name = "Германия", outbound }, HttpStatusCode.Created);
        return created.GetProperty("server").GetProperty("id").GetString()!;
    }
}
