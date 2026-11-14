using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using FlowGate.Shared.Bll.Processes;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/servers</c>: сценарий попапа «ссылка → разобрать → JSON-редактор → сохранить»
/// </summary>
public sealed class ServerEndpointsTests : IDisposable
{
    private const string Link =
        "vless://34f2bd76-fba8-4322-945c-500653e90c8f@132.243.213.13:443?type=xhttp&security=reality&sni=botapi.max.ru&fp=firefox&pbk=PUB&sid=e308&spx=%2F#cascade-out-2";

    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Разбор → проверка → создание → правка → удаление
    /// </summary>
    [Fact]
    public async Task EditorFlow_ParseValidateSaveEditDelete_Works()
    {
        using var client = _factory.CreateClient();

        var draft = await ApiJson.PostAsync(client, "/api/servers/parse", new { text = Link }, HttpStatusCode.OK);
        var outbound = JsonNode.Parse(draft.GetProperty("outbound").GetRawText())!.AsObject();
        var validation = await ApiJson.PostAsync(client, "/api/servers/validate", new { outbound }, HttpStatusCode.OK);
        var created = await ApiJson.PostAsync(client, "/api/servers", new { name = draft.GetProperty("name").GetString(), outbound }, HttpStatusCode.Created);
        var id = created.GetProperty("server").GetProperty("id").GetString();

        outbound["settings"]!["port"] = 8443;
        using var update = await client.PutAsJsonAsync($"/api/servers/{id}", new { name = "Переименован", outbound }, TestContext.Current.CancellationToken);
        var list = await ApiJson.GetAsync(client, "/api/servers");
        using var delete = await client.DeleteAsync($"/api/servers/{id}", TestContext.Current.CancellationToken);
        var after = await ApiJson.GetAsync(client, "/api/servers");

        Assert.Equal("cascade-out-2", draft.GetProperty("name").GetString());
        Assert.Equal("xhttp", outbound["streamSettings"]!["network"]!.GetValue<string>());
        Assert.True(validation.GetProperty("valid").GetBoolean());
        Assert.False(validation.GetProperty("checkedByXray").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(8443, list[0].GetProperty("port").GetInt32());
        Assert.Equal("Переименован", list[0].GetProperty("name").GetString());
        Assert.Equal("reality", list[0].GetProperty("security").GetString());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(0, after.GetArrayLength());
    }

    /// <summary>
    ///     Предпросмотр ссылок отдаёт сервер с ключом и ничего не сохраняет; пустой текст — 400
    /// </summary>
    [Fact]
    public async Task Preview_Links_ReturnsServersWithoutSaving()
    {
        using var client = _factory.CreateClient();

        var preview = await ApiJson.PostAsync(client, "/api/servers/preview", new { text = $"{Link}\nvless://broken" }, HttpStatusCode.OK);
        var empty = await ApiJson.PostAsync(client, "/api/servers/preview", new { text = " " }, HttpStatusCode.BadRequest);
        var list = await ApiJson.GetAsync(client, "/api/servers");

        var server = Assert.Single(preview.GetProperty("servers").EnumerateArray());
        Assert.Equal("cascade-out-2", server.GetProperty("name").GetString());
        Assert.Equal("132.243.213.13", server.GetProperty("address").GetString());
        Assert.Equal(64, server.GetProperty("importKey").GetString()!.Length);
        Assert.Equal(1, preview.GetProperty("rejected").GetArrayLength());
        Assert.Equal("VALIDATION_ERROR", empty.GetProperty("errorCode").GetString());
        Assert.Equal(0, list.GetArrayLength());
    }

    /// <summary>
    ///     Установленный Xray отклоняет outbound — 400 с ошибками в details, как в попапе на скриншоте
    /// </summary>
    [Fact]
    public async Task Create_XrayRejectsOutbound_Returns400WithDetails()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_factory.XrayBinaryPath)!);
        File.WriteAllText(_factory.XrayBinaryPath, FakeXrayBinary.Content("26.10.31"));
        _factory.ProcessRunner.Handler = (_, arguments) => arguments.Contains("-test")
            ? new ProcessResult(23, "Failed to start: main: failed to load config files > infra/conf: unknown field \"probeUrl\"", string.Empty)
            : new ProcessResult(0, FakeXrayBinary.VersionOutput("26.10.31"), string.Empty);
        using var client = _factory.CreateClient();
        var outbound = new { protocol = "vless", settings = new { address = "a.example", port = 443, id = "x", encryption = "none" } };

        var validation = await ApiJson.PostAsync(client, "/api/servers/validate", new { outbound }, HttpStatusCode.OK);
        var error = await ApiJson.PostAsync(client, "/api/servers", new { name = "X", outbound }, HttpStatusCode.BadRequest);

        Assert.False(validation.GetProperty("valid").GetBoolean());
        Assert.True(validation.GetProperty("checkedByXray").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", error.GetProperty("errorCode").GetString());
        Assert.Contains("probeUrl", error.GetProperty("details")[0].GetString());
    }

    /// <summary>
    ///     Неразбираемая ссылка — 400 с причиной
    /// </summary>
    [Fact]
    public async Task Parse_BrokenLink_Returns400WithReason()
    {
        using var client = _factory.CreateClient();

        var error = await ApiJson.PostAsync(client, "/api/servers/parse", new { text = "vless://id@a.example:443?security=reality" }, HttpStatusCode.BadRequest);

        Assert.Contains("pbk", error.GetProperty("details")[0].GetString());
    }

    /// <summary>
    ///     Пакетный импорт добавляет новые и пропускает повторы
    /// </summary>
    [Fact]
    public async Task Import_Twice_SkipsDuplicates()
    {
        using var client = _factory.CreateClient();
        var text = $"{Link}\ntrojan://p@de.example.net:443#DE";

        var first = await ApiJson.PostAsync(client, "/api/servers/import", new { text }, HttpStatusCode.OK);
        var second = await ApiJson.PostAsync(client, "/api/servers/import", new { text }, HttpStatusCode.OK);

        Assert.Equal(2, first.GetProperty("added").GetArrayLength());
        Assert.Equal(2, second.GetProperty("skipped").GetInt32());
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
