using System.Net;
using System.Net.Sockets;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Implementations.Xray;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Servers;
using FlowGate.Tests.Common.Processes;
using FlowGate.Tests.Unit.Connections;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Тесты <see cref="ServerPingService"/>: временный Xray подменён, порты его socks-inbound открывает сам тест
/// </summary>
public sealed class ServerPingServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly IXrayVersionProvider _versionProvider = Substitute.For<IXrayVersionProvider>();
    private readonly IXrayProcessManager _processManager = Substitute.For<IXrayProcessManager>();
    private readonly IOutboundProbe _probe = Substitute.For<IOutboundProbe>();
    private readonly List<TcpListener> _listeners = [];
    private readonly List<FakeManagedProcess> _processes = [];
    private readonly ServerProvider _servers;
    private readonly ServerPingService _sut;
    private JsonObject? _startedConfig;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerPingServiceTests"/>
    /// </summary>
    public ServerPingServiceTests()
    {
        var storeFactory = new JsonFileStoreFactory(Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }));
        _servers = new ServerProvider(storeFactory);
        _versionProvider.IsInstalled.Returns(true);
        _processManager
            .Start(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>())
            .Returns(call => StartFakeXray(call.Arg<IReadOnlyList<string>>(), call.Arg<Action<string>>()));

        _sut = new ServerPingService(
            _servers,
            _versionProvider,
            _processManager,
            new XrayConfigBuilder(),
            _probe,
            Microsoft.Extensions.Options.Options.Create(new PingOptions { StartTimeoutSeconds = 1 }),
            Microsoft.Extensions.Options.Options.Create(new XrayOptions { WorkDirectory = Path.Combine(_directory, "work") }),
            TimeProvider.System,
            NullLogger<ServerPingService>.Instance);

        _servers.AddAsync([ConnectionTestData.Server("srv-de"), ConnectionTestData.Server("srv-nl")], CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    ///     Живой и мёртвый серверы: задержка у одного, ошибка у другого, порядок как в запросе, Xray убит, файл удалён
    /// </summary>
    [Fact]
    public async Task PingAsync_AliveAndDead_ReturnsResultsAndCleansUp()
    {
        _probe.ProbeAsync(Arg.Any<int>(), Arg.Any<Uri>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<int>() == InboundPort(0)
                ? Task.FromException<TimeSpan>(new TimeoutException("нет ответа за 5 с"))
                : Task.FromResult(TimeSpan.FromMilliseconds(42.4)));

        var results = await _sut.PingAsync(new ServerPingRequest(["srv-nl", "srv-de", "srv-ghost"]), Token);

        Assert.Equal(
            [
                new ServerPingResultDto("srv-nl", null, "нет ответа за 5 с"),
                new ServerPingResultDto("srv-de", 42, null),
                new ServerPingResultDto("srv-ghost", null, "Сервер не найден"),
            ],
            results);
        Assert.True(Assert.Single(_processes).WasKilled);
        Assert.Empty(Directory.GetFiles(Path.Combine(_directory, "work")));
    }

    /// <summary>
    ///     Временный конфиг: socks-inbound на каждый сервер и правило в его outbound
    /// </summary>
    [Fact]
    public async Task PingAsync_BuildsSocksInboundPerServer()
    {
        _probe.ProbeAsync(Arg.Any<int>(), Arg.Any<Uri>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(TimeSpan.FromMilliseconds(10));

        await _sut.PingAsync(new ServerPingRequest(["srv-de", "srv-nl"]), Token);

        var inbounds = _startedConfig!["inbounds"]!.AsArray();
        Assert.Equal(2, inbounds.Count);
        Assert.All(inbounds, inbound => Assert.Equal("socks", inbound!["protocol"]!.GetValue<string>()));
        Assert.Equal(
            ["ping-out-0", "ping-out-1"],
            _startedConfig["routing"]!["rules"]!.AsArray().Select(rule => rule!["outboundTag"]!.GetValue<string>()));
    }

    /// <summary>
    ///     Xray завершился, не открыв порты, — у всех серверов ошибка с последней строкой вывода
    /// </summary>
    [Fact]
    public async Task PingAsync_XrayExitsOnStart_ReportsReason()
    {
        _processManager
            .Start(Arg.Any<IReadOnlyList<string>>(), Arg.Any<Action<string>>())
            .Returns(call =>
            {
                var process = new FakeManagedProcess(1, new ProcessStartRequest("xray", []), call.Arg<Action<string>>());
                process.WriteLine("main: failed to load config files > bad outbound");
                process.Exit(23);
                _processes.Add(process);
                return process;
            });

        var results = await _sut.PingAsync(new ServerPingRequest(["srv-de"]), Token);

        Assert.Equal("Xray не запустился: main: failed to load config files → bad outbound", Assert.Single(results).Error);
    }

    /// <summary>
    ///     Xray не установлен — 409
    /// </summary>
    [Fact]
    public async Task PingAsync_XrayNotInstalled_ThrowsConflict()
    {
        _versionProvider.IsInstalled.Returns(false);

        await Assert.ThrowsAsync<ConflictException>(() => _sut.PingAsync(new ServerPingRequest(["srv-de"]), Token));
    }

    /// <summary>
    ///     Пустой список и слишком длинный — 400
    /// </summary>
    [Fact]
    public async Task PingAsync_InvalidCount_ThrowsValidation()
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.PingAsync(new ServerPingRequest([]), Token));
        await Assert.ThrowsAsync<RequestValidationException>(
            () => _sut.PingAsync(new ServerPingRequest(Enumerable.Range(0, 26).Select(index => $"srv-{index}").ToList()), Token));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _sut.Dispose();
        _listeners.ForEach(listener => listener.Stop());
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private int InboundPort(int index) => _startedConfig!["inbounds"]![index]!["port"]!.GetValue<int>();

    private FakeManagedProcess StartFakeXray(IReadOnlyList<string> arguments, Action<string> onOutputLine)
    {
        _startedConfig = JsonNode.Parse(File.ReadAllText(arguments[^1]))!.AsObject();
        foreach (var inbound in _startedConfig["inbounds"]!.AsArray())
        {
            var listener = new TcpListener(IPAddress.Loopback, inbound!["port"]!.GetValue<int>());
            listener.Start();
            _listeners.Add(listener);
        }

        var process = new FakeManagedProcess(1, new ProcessStartRequest("xray", arguments), onOutputLine);
        _processes.Add(process);
        return process;
    }
}
