using System.Text.Json.Nodes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Contracts.Connections;
using FlowGate.Contracts.Servers;
using FlowGate.Dal.Implementations.Connections;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Exceptions;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Тесты <see cref="ServerService"/>: разбор, ручные серверы, защита серверов подписки, импорт
/// </summary>
public sealed class ServerServiceTests : IDisposable
{
    private const string TrojanLink = "trojan://p@de.example.net:443?sni=de.example.net#DE";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly IOutboundValidator _validator = Substitute.For<IOutboundValidator>();
    private readonly ServerProvider _servers;
    private readonly ConnectionProvider _connections;
    private readonly ServerService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ServerServiceTests"/>
    /// </summary>
    public ServerServiceTests()
    {
        var storeFactory = new JsonFileStoreFactory(Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }));
        _servers = new ServerProvider(storeFactory);
        _connections = new ConnectionProvider(storeFactory);
        _validator.ValidateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>()).Returns(new OutboundValidationResult([], CheckedByXray: true));
        _sut = new ServerService(_servers, _connections, new ServerImportParser(), _validator, TimeProvider.System, NullLogger<ServerService>.Instance);
    }

    /// <summary>
    ///     Разбор ссылки ничего не сохраняет и предупреждает, если серверов несколько
    /// </summary>
    [Fact]
    public async Task Parse_SeveralLinks_ReturnsFirstWithWarningAndSavesNothing()
    {
        var draft = _sut.Parse(new ServerParseRequest($"{TrojanLink}\nvless://id@nl.example.net:443#NL"));

        Assert.Equal("DE", draft.Name);
        Assert.Contains(draft.Warnings, warning => warning.Contains("Найдено серверов: 2"));
        Assert.Empty(await _servers.GetAllAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Неразбираемая ссылка — 400 с причинами в Details
    /// </summary>
    [Fact]
    public void Parse_BrokenLink_ThrowsWithDetails()
    {
        var exception = Assert.Throws<RequestValidationException>(() => _sut.Parse(new ServerParseRequest("vless://id@example.com:443?type=kcp")));

        Assert.Contains("kcp", Assert.Single(exception.Details!));
    }

    /// <summary>
    ///     Ручной сервер создаётся без tag, правится и удаляется
    /// </summary>
    [Fact]
    public async Task CreateUpdateDelete_ManualServer_Works()
    {
        var draft = _sut.Parse(new ServerParseRequest(TrojanLink));
        var outbound = draft.Outbound;
        outbound["tag"] = "should-be-removed";

        var created = await _sut.CreateAsync(new ServerSaveRequest("Германия", outbound), TestContext.Current.CancellationToken);
        var edited = (JsonObject)created.Outbound.DeepClone();
        edited["settings"]!["port"] = 8443;
        var updated = await _sut.UpdateAsync(created.Server.Id, new ServerSaveRequest("Германия 2", edited), TestContext.Current.CancellationToken);
        await _sut.DeleteAsync(created.Server.Id, TestContext.Current.CancellationToken);

        Assert.StartsWith("srv-", created.Server.Id);
        Assert.False(created.Outbound.ContainsKey("tag"));
        Assert.True(created.Editable);
        Assert.Equal(8443, updated.Server.Port);
        Assert.Equal("Германия 2", updated.Server.Name);
        Assert.Empty(await _servers.GetAllAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Outbound с ошибками проверки не сохраняется, ошибки уходят в Details
    /// </summary>
    [Fact]
    public async Task CreateAsync_InvalidOutbound_ThrowsWithXrayErrors()
    {
        _validator.ValidateAsync(Arg.Any<JsonObject>(), Arg.Any<CancellationToken>())
            .Returns(new OutboundValidationResult(["/observatory/probeUrl: field does not exist"], CheckedByXray: true));

        var exception = await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(
            new ServerSaveRequest("X", new JsonObject { ["protocol"] = "vless", ["settings"] = new JsonObject() }),
            TestContext.Current.CancellationToken));

        Assert.Contains("probeUrl", Assert.Single(exception.Details!));
        Assert.Empty(await _servers.GetAllAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Сервер подписки нельзя изменить или удалить вручную
    /// </summary>
    [Fact]
    public async Task UpdateAndDelete_SubscriptionServer_Rejected()
    {
        var outbound = _sut.Parse(new ServerParseRequest(TrojanLink)).Outbound;
        var server = new Server("sub-s1-abc", "DE", outbound, ServerSourceKind.Subscription, "s1", ServerImportKey.Compute(outbound), DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        await _servers.AddAsync([server], TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<RequestValidationException>(
            () => _sut.UpdateAsync(server.Id, new ServerSaveRequest("X", outbound), TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.DeleteAsync(server.Id, TestContext.Current.CancellationToken));
        Assert.False((await _sut.GetAsync(server.Id, TestContext.Current.CancellationToken)).Editable);
    }

    /// <summary>
    ///     Повторный импорт тех же ссылок пропускает существующие серверы
    /// </summary>
    [Fact]
    public async Task ImportAsync_Twice_SkipsExisting()
    {
        var text = $"{TrojanLink}\nvless://id@nl.example.net:443#NL\nvless://broken";

        var first = await _sut.ImportAsync(new ServerImportRequest(text), TestContext.Current.CancellationToken);
        var second = await _sut.ImportAsync(new ServerImportRequest(text), TestContext.Current.CancellationToken);

        Assert.Equal(2, first.Added.Count);
        Assert.Single(first.Rejected);
        Assert.Empty(second.Added);
        Assert.Equal(2, second.Skipped);
        Assert.Equal(2, (await _servers.GetAllAsync(TestContext.Current.CancellationToken)).Count);
    }

    /// <summary>
    ///     Сервер, выбранный в соединении, удалить нельзя — 409 с именем соединения
    /// </summary>
    [Fact]
    public async Task DeleteAsync_ServerUsedByConnection_ThrowsConflict()
    {
        var created = await _sut.CreateAsync(new ServerSaveRequest("Германия", _sut.Parse(new ServerParseRequest(TrojanLink)).Outbound), TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UnixEpoch;
        await _connections.AddAsync(
            new Connection("con-1", "YouTube", ConnectionKind.Socks, created.Server.Id, 10800, "Proxy0", null, now, now),
            TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<ConflictException>(() => _sut.DeleteAsync(created.Server.Id, TestContext.Current.CancellationToken));

        Assert.Contains("«YouTube»", exception.Message);
        Assert.NotNull(await _servers.GetAsync(created.Server.Id, TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
