using System.Net;

namespace FlowGate.Tests.Integration.Hosting;

/// <summary>
///     Тесты раздачи статики фронта и SPA-fallback
/// </summary>
public sealed class FrontendTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><title>FlowGate</title>";
    private const string AppJs = "console.log('flowgate');";

    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FrontendTests"/>
    /// </summary>
    public FrontendTests()
    {
        Directory.CreateDirectory(Path.Combine(_factory.WebRootPath, "assets"));
        File.WriteAllText(Path.Combine(_factory.WebRootPath, "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_factory.WebRootPath, "assets", "app.js"), AppJs);
    }

    /// <summary>
    ///     Корень отдаёт index.html
    /// </summary>
    [Fact]
    public async Task GetRoot_FrontendPresent_ReturnsIndex()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync("/", TestContext.Current.CancellationToken);

        Assert.Equal(IndexHtml, body);
    }

    /// <summary>
    ///     Файлы фронта отдаются как есть
    /// </summary>
    [Fact]
    public async Task GetAsset_FrontendPresent_ReturnsFile()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync("/assets/app.js", TestContext.Current.CancellationToken);

        Assert.Equal(AppJs, body);
    }

    /// <summary>
    ///     Клиентский маршрут фронта отдаёт index.html
    /// </summary>
    [Fact]
    public async Task GetClientRoute_FrontendPresent_ReturnsIndex()
    {
        using var client = _factory.CreateClient();

        var body = await client.GetStringAsync("/routes/dns", TestContext.Current.CancellationToken);

        Assert.Equal(IndexHtml, body);
    }

    /// <summary>
    ///     Неизвестный путь API не подменяется index.html
    /// </summary>
    [Fact]
    public async Task GetUnknownApi_FrontendPresent_Returns404()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync("/api/unknown", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();
}
