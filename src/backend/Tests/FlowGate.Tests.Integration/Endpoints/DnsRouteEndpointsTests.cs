using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

using FlowGate.Tests.Common.DomainSources;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Тесты <c>/api/domain-sources</c> и <c>/api/dns-routes</c>: источники всех видов, каталог, маршрут на соединение,
///     применение в подменённом Keenetic
/// </summary>
public sealed class DnsRouteEndpointsTests : IDisposable
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Локальный список → маршрут на socks → группа и правило в Keenetic; соединение и список защищены от удаления;
    ///     удаление маршрута убирает всё из Keenetic
    /// </summary>
    [Fact]
    public async Task LocalSourceRoute_Lifecycle_Works()
    {
        using var client = _factory.CreateClient();
        var connectionId = await CreateConnectionAsync(client);
        var source = await ApiJson.PostAsync(client, "/api/domain-sources", new { name = "Мои", kind = "Local" }, HttpStatusCode.Created);
        var sourceId = source.GetProperty("id").GetString();
        using var save = await client.PutAsJsonAsync(
            $"/api/domain-sources/{sourceId}/local-lists/telegram",
            new { text = "telegram.org\n91.108.56.1/22\n" },
            TestContext.Current.CancellationToken);

        var route = await ApiJson.PostAsync(client, "/api/dns-routes", new { sourceId, listName = "telegram", connectionId }, HttpStatusCode.Created);
        await ApiJson.WaitForAllJobsAsync(client);
        var routes = await ApiJson.GetAsync(client, "/api/dns-routes");
        var deleteConnection = await ApiJson.DeleteAsync(client, $"/api/connections/{connectionId}", HttpStatusCode.Conflict);
        var deleteList = await ApiJson.DeleteAsync(client, $"/api/domain-sources/{sourceId}/local-lists/telegram", HttpStatusCode.Conflict);
        var groups = _factory.Keenetic.Groups;
        var rules = _factory.Keenetic.Routes;
        await ApiJson.DeleteAsync(client, $"/api/dns-routes/{route.GetProperty("id").GetString()}", HttpStatusCode.NoContent);
        await ApiJson.WaitForAllJobsAsync(client);

        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        Assert.Equal(["telegram.org", "91.108.56.0/22"], groups["fg-telegram"]);
        Assert.Equal("Proxy0", Assert.Single(rules).Interface);
        Assert.Equal("Enabled", routes[0].GetProperty("state").GetString());
        Assert.Equal("CONFLICT", deleteConnection.GetProperty("errorCode").GetString());
        Assert.Equal("CONFLICT", deleteList.GetProperty("errorCode").GetString());
        Assert.Empty(_factory.Keenetic.Groups);
        Assert.Empty(_factory.Keenetic.Routes);
    }

    /// <summary>
    ///     GitHub-репозиторий: каталог из tarball коммита; повторное обновление при том же sha архив не качает
    /// </summary>
    [Fact]
    public async Task GitHubRepository_RefreshTwice_DownloadsOnce()
    {
        var archive = RepositoryArchive.Create("owner-lists-0123456", new Dictionary<string, string>
        {
            ["data/nintendo.txt"] = "nintendo.com\nnintendo.net\n",
            ["data/telegram"] = "telegram.org\n91.108.4.0/22\n",
        });
        var tarballRequests = 0;
        _factory.GitHub.Extra = request => request.Uri?.AbsolutePath switch
        {
            "/repos/owner/lists/commits/HEAD" => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Sha) },
            $"/repos/owner/lists/tarball/{Sha}" => Count(ref tarballRequests, FakeHttpMessageHandler.Bytes(archive)),
            _ => null,
        };
        using var client = _factory.CreateClient();

        var created = await ApiJson.PostAsync(client, "/api/domain-sources", new { name = "Свой", kind = "GitHubRepository", repository = "https://github.com/owner/lists" }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString();
        await ApiJson.WaitForAllJobsAsync(client);
        var refresh = await ApiJson.PostAsync(client, $"/api/domain-sources/{id}/refresh", new { }, HttpStatusCode.Accepted);
        await ApiJson.WaitForJobAsync(client, refresh.GetProperty("id").GetString()!);
        var source = await ApiJson.GetAsync(client, $"/api/domain-sources/{id}");
        var lists = await ApiJson.GetAsync(client, $"/api/domain-sources/{id}/lists");
        var preview = await ApiJson.GetAsync(client, $"/api/domain-sources/{id}/lists/telegram");

        Assert.Equal("owner/lists", created.GetProperty("repository").GetString());
        Assert.Equal(1, tarballRequests);
        Assert.Equal(Sha, source.GetProperty("revision").GetString());
        Assert.Equal("Ok", source.GetProperty("lastRefresh").GetProperty("status").GetString());
        Assert.Equal(["nintendo", "telegram"], lists.EnumerateArray().Select(list => list.GetProperty("name").GetString()));
        Assert.Equal((1, 1), (preview.GetProperty("domains").GetInt32(), preview.GetProperty("ipv4").GetInt32()));
    }

    /// <summary>
    ///     geosite.dat по прямой ссылке: первая загрузка, затем условный запрос с ETag и ответ 304
    /// </summary>
    [Fact]
    public async Task GeositeByUrl_SecondRefresh_UsesETag()
    {
        var dat = DatFileBuilder.Geosite(("YOUTUBE", [(DatFileBuilder.Domain, "youtube.com", [])]));
        _factory.DomainSourceHttp.Responder = request =>
        {
            if (request.Headers.TryGetValue("If-None-Match", out var etag) && etag == "\"v1\"")
            {
                return new HttpResponseMessage(HttpStatusCode.NotModified);
            }

            var response = FakeHttpMessageHandler.Bytes(dat);
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue("\"v1\"");
            return response;
        };
        using var client = _factory.CreateClient();

        var created = await ApiJson.PostAsync(client, "/api/domain-sources", new { name = "CDN", kind = "GeositeDat", url = "https://cdn.example.net/geosite.dat?token=secret" }, HttpStatusCode.Created);
        var id = created.GetProperty("id").GetString();
        await ApiJson.WaitForAllJobsAsync(client);
        var refresh = await ApiJson.PostAsync(client, $"/api/domain-sources/{id}/refresh", new { }, HttpStatusCode.Accepted);
        var job = await ApiJson.WaitForJobAsync(client, refresh.GetProperty("id").GetString()!);
        var lists = await ApiJson.GetAsync(client, $"/api/domain-sources/{id}/lists?query=you");

        Assert.Equal("Succeeded", job.GetProperty("status").GetString());
        Assert.Contains(job.GetProperty("steps").EnumerateArray(), step => step.GetProperty("message").GetString()!.Contains("не изменился"));
        Assert.DoesNotContain(job.GetProperty("steps").EnumerateArray(), step => step.GetProperty("message").GetString()!.Contains("secret"));
        Assert.Equal(2, _factory.DomainSourceHttp.Requests.Count);
        Assert.Equal("youtube", lists[0].GetProperty("name").GetString());
    }

    /// <summary>
    ///     Встроенный источник удалить нельзя; некорректные параметры — 400
    /// </summary>
    [Fact]
    public async Task Sources_BuiltInAndInvalid_Rejected()
    {
        using var client = _factory.CreateClient();

        var all = await ApiJson.GetAsync(client, "/api/domain-sources");
        var deleteBuiltIn = await ApiJson.DeleteAsync(client, "/api/domain-sources/v2fly", HttpStatusCode.BadRequest);
        var badRepository = await ApiJson.PostAsync(client, "/api/domain-sources", new { name = "X", kind = "GitHubRepository", repository = "not a repo" }, HttpStatusCode.BadRequest);

        Assert.Equal(["v2fly", "v2fly-geoip"], all.EnumerateArray().Select(source => source.GetProperty("id").GetString()));
        Assert.False(all[0].GetProperty("downloaded").GetBoolean());
        Assert.Equal("VALIDATION_ERROR", deleteBuiltIn.GetProperty("errorCode").GetString());
        Assert.Equal("VALIDATION_ERROR", badRepository.GetProperty("errorCode").GetString());
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static HttpResponseMessage Count(ref int counter, HttpResponseMessage response)
    {
        counter++;
        return response;
    }

    private static async Task<string> CreateConnectionAsync(HttpClient client)
    {
        var draft = await ApiJson.PostAsync(client, "/api/servers/parse", new { text = "trojan://p@de.example.net:443?sni=de.example.net#DE" }, HttpStatusCode.OK);
        var outbound = JsonNode.Parse(draft.GetProperty("outbound").GetRawText());
        var server = await ApiJson.PostAsync(client, "/api/servers", new { name = "DE", outbound }, HttpStatusCode.Created);
        var connection = await ApiJson.PostAsync(
            client,
            "/api/connections",
            new { name = "Video", kind = "Socks", serverId = server.GetProperty("server").GetProperty("id").GetString() },
            HttpStatusCode.Created);
        return connection.GetProperty("id").GetString()!;
    }
}
