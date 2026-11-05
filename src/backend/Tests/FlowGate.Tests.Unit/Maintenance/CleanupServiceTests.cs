using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using FlowGate.Bll.Implementations.Keenetic;
using FlowGate.Bll.Implementations.Maintenance;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Dal.Implementations.DnsRoutes;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Ndms;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Keenetic;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Keenetic;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Maintenance;

/// <summary>
///     Тесты <see cref="CleanupService"/>: подменный Keenetic со своими и чужими интерфейсами, группами и правилами
/// </summary>
public sealed class CleanupServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeKeeneticRci _keenetic = new();
    private readonly IXrayProcessManager _xray = Substitute.For<IXrayProcessManager>();
    private readonly NdmsOperationQueue _queue;
    private readonly KeeneticDnsService _dns;
    private readonly KeeneticInterfaceService _interfaces;
    private readonly DnsRouteProvider _routes;
    private readonly CleanupService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="CleanupServiceTests"/>
    /// </summary>
    public CleanupServiceTests()
    {
        _queue = new NdmsOperationQueue(MsOptions.Create(new KeeneticOptions()), NullLogger<NdmsOperationQueue>.Instance);
        var rci = new KeeneticRciClient(new HttpClient(_keenetic.Handler) { BaseAddress = new Uri("http://127.0.0.1:79/rci/") });
        _dns = new KeeneticDnsService(rci, _queue, MsOptions.Create(new DomainSourceOptions()), NullLogger<KeeneticDnsService>.Instance);
        _interfaces = new KeeneticInterfaceService(rci, _queue, NullLogger<KeeneticInterfaceService>.Instance);
        _routes = new DnsRouteProvider(new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = _directory })));
        _sut = Create(_dns);

        _keenetic.AddInterface("Proxy0", RciFixtures.ShowInterfaceProxy);
        _keenetic.AddInterface("Wireguard0", RciFixtures.ShowInterfaceWireguard);
        _keenetic.AddInterface("Proxy5", RciFixtures.ShowInterfaceProxy);
        _keenetic.SetInterfaceField("Proxy5", "description", "Мой прокси");

        _keenetic.AddGroup("fg-youtube", "FlowGate: youtube (v2fly)", "youtube.com");
        _keenetic.AddGroup("fg-youtube-2", "FlowGate: youtube (v2fly) 2", "googlevideo.com");
        _keenetic.AddGroup("fg-mine", "", "mine.example");
        _keenetic.AddGroup("my-list", "Мой список", "example.org");
        _keenetic.ClearCommands();
    }

    /// <summary>
    ///     Удаляются только объекты FlowGate: правила снимаются до удаления групп, чужие группы и интерфейсы остаются
    /// </summary>
    [Fact]
    public async Task RunAsync_MixedState_RemovesOnlyFlowGateObjects()
    {
        await AddRuleAsync("fg-youtube", "Proxy0");
        await AddRuleAsync("fg-youtube-2", "Proxy5");
        await AddRuleAsync("my-list", "Proxy5");

        var report = await _sut.RunAsync(dryRun: false, Token);

        Assert.True(report.Succeeded);
        Assert.Equal((2, 2, 2), (report.Routes, report.Groups, report.Interfaces));
        Assert.Equal(["fg-mine", "my-list"], _keenetic.Groups.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(new FakeDnsRoute("my-list", "Proxy5", true, false), Assert.Single(_keenetic.Routes));
        Assert.Equal(["Proxy5"], _keenetic.InterfaceNames);
        Assert.True(
            _keenetic.Commands.ToList().FindLastIndex(command => command.StartsWith("no dns-proxy route", StringComparison.Ordinal))
            < _keenetic.Commands.ToList().FindIndex(command => command.StartsWith("no object-group", StringComparison.Ordinal)));
        await _xray.Received(1).KillStrangersAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Повторная очистка ничего не отправляет в Keenetic
    /// </summary>
    [Fact]
    public async Task RunAsync_SecondRun_SendsNoCommands()
    {
        await _sut.RunAsync(dryRun: false, Token);
        _keenetic.ClearCommands();

        var report = await _sut.RunAsync(dryRun: false, Token);

        Assert.Empty(report.Commands);
        Assert.Empty(_keenetic.Commands);
    }

    /// <summary>
    ///     Пробный запуск показывает команды, но ничего не меняет и не трогает xray
    /// </summary>
    [Fact]
    public async Task RunAsync_DryRun_ChangesNothing()
    {
        var report = await _sut.RunAsync(dryRun: true, Token);

        Assert.Equal(
            ["no object-group fqdn fg-youtube", "no object-group fqdn fg-youtube-2", "no interface Proxy0", "no interface Wireguard0"],
            report.Commands);
        Assert.Empty(_keenetic.Commands);
        Assert.Equal(4, _keenetic.Groups.Count);
        await _xray.DidNotReceive().KillStrangersAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Ошибка шага DNS не мешает удалить интерфейсы; итог с ошибкой
    /// </summary>
    [Fact]
    public async Task RunAsync_DnsUnavailable_RemovesInterfacesAndReportsError()
    {
        var dns = Substitute.For<IKeeneticDnsService>();
        dns.GetStateAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new KeeneticUnavailableException("RCI недоступен"));
        var sut = Create(dns);

        var report = await sut.RunAsync(dryRun: false, Token);

        Assert.False(report.Succeeded);
        Assert.Contains("RCI недоступен", Assert.Single(report.Errors));
        Assert.Equal(["Proxy5"], _keenetic.InterfaceNames);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _queue.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private CleanupService Create(IKeeneticDnsService dns) =>
        new(_xray, dns, _interfaces, _routes, NullLogger<CleanupService>.Instance);

    private async Task AddRuleAsync(string group, string interfaceName)
    {
        await _dns.ExecuteAsync("test", [$"dns-proxy route object-group {group} {interfaceName} auto"], report: null, Token);
        _keenetic.ClearCommands();
    }
}
