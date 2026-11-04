using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using FlowGate.Bll.Implementations;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Keenetic;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;
using FlowGate.Tests.Common.Processes;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Integration;

/// <summary>
///     Хост FlowGate для интеграционных тестов: RCI, GitHub и запуск процессов подменены, каталоги — временные
/// </summary>
public sealed class FlowGateWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flowgate-it", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Подменённый RCI; по умолчанию отвечает реальным show/version
    /// </summary>
    public FakeHttpMessageHandler Rci { get; } = FakeHttpMessageHandler.RespondJson(RciFixtures.ShowVersion);

    /// <summary>
    ///     Подменённый GitHub с релизами Xray
    /// </summary>
    public FakeXrayGitHub GitHub { get; } = new();

    /// <summary>
    ///     Подменённый HTTP провайдеров подписок; по умолчанию отдаёт base64-список из трёх серверов
    /// </summary>
    public FakeHttpMessageHandler SubscriptionHttp { get; } = new(_ => FakeHttpMessageHandler.Json(SubscriptionFixtures.Base64List()));

    /// <summary>
    ///     Подменный запуск команд (ведёт себя как Xray)
    /// </summary>
    public FakeProcessRunner ProcessRunner { get; } = new();

    /// <summary>
    ///     Подменный запуск долгоживущих процессов
    /// </summary>
    public FakeProcessLauncher ProcessLauncher { get; } = new();

    /// <summary>
    ///     Каталог данных приложения
    /// </summary>
    public string DataDirectory => Path.Combine(_root, "data");

    /// <summary>
    ///     Каталог статики фронта (создаётся тестом при необходимости)
    /// </summary>
    public string WebRootPath => Path.Combine(_root, "www");

    /// <summary>
    ///     Путь к бинарнику Xray
    /// </summary>
    public string XrayBinaryPath => Path.Combine(_root, "bin", "xray");

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("FlowGate:DataDirectory", DataDirectory);
        builder.UseSetting("FlowGate:WebRootPath", WebRootPath);
        builder.UseSetting("Keenetic:RciBaseUrl", "http://127.0.0.1:79/rci/");
        builder.UseSetting("Xray:BinaryPath", XrayBinaryPath);
        builder.UseSetting("Xray:WorkDirectory", Path.Combine(_root, "tmp"));
        builder.UseSetting("Xray:StartTimeoutSeconds", "0");
        builder.UseSetting("Xray:MinFreeSpaceMegabytes", "0");

        builder.ConfigureTestServices(services =>
        {
            services.AddKeeneticRciClient().ConfigurePrimaryHttpMessageHandler(() => Rci);
            services.AddGitHubReleasesClient().ConfigurePrimaryHttpMessageHandler(() => GitHub.Handler);
            services.AddSubscriptionHttpClient().ConfigurePrimaryHttpMessageHandler(() => SubscriptionHttp);
            services.AddSingleton<IProcessRunner>(ProcessRunner);
            services.AddSingleton<IProcessLauncher>(ProcessLauncher);
        });
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
