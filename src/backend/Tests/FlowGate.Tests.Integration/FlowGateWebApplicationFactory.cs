using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using FlowGate.Shared.Keenetic;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Integration;

/// <summary>
///     Хост FlowGate для интеграционных тестов: RCI подменён, каталоги данных и фронта — временные
/// </summary>
public sealed class FlowGateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flowgate-it", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Подменённый RCI; по умолчанию отвечает реальным show/version
    /// </summary>
    public FakeHttpMessageHandler Rci { get; } = FakeHttpMessageHandler.RespondJson(RciFixtures.ShowVersion);

    /// <summary>
    ///     Каталог данных приложения
    /// </summary>
    public string DataDirectory => Path.Combine(_root, "data");

    /// <summary>
    ///     Каталог статики фронта (создаётся тестом при необходимости)
    /// </summary>
    public string WebRootPath => Path.Combine(_root, "www");

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("FlowGate:DataDirectory", DataDirectory);
        builder.UseSetting("FlowGate:WebRootPath", WebRootPath);
        builder.UseSetting("Keenetic:RciBaseUrl", "http://127.0.0.1:79/rci/");

        builder.ConfigureTestServices(services =>
            services.AddKeeneticRciClient().ConfigurePrimaryHttpMessageHandler(() => Rci));
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
