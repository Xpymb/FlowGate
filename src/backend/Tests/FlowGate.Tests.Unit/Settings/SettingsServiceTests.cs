using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.Settings;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.Tests.Unit.Connections;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Settings;

/// <summary>
///     Тесты <see cref="SettingsService"/>: значения по умолчанию, проверка, сохранение и применение
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly IUpdateProxyService _updateProxy = Substitute.For<IUpdateProxyService>();
    private readonly IXrayConfigService _xrayConfig = Substitute.For<IXrayConfigService>();
    private readonly SettingsProvider _settings;
    private readonly SettingsService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SettingsServiceTests"/>
    /// </summary>
    public SettingsServiceTests()
    {
        var storeFactory = new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = _directory }));
        _settings = new SettingsProvider(storeFactory);
        var servers = new ServerProvider(storeFactory);
        servers.AddAsync([ConnectionTestData.Server()], CancellationToken.None).GetAwaiter().GetResult();

        _sut = new SettingsService(
            _settings,
            servers,
            _updateProxy,
            _xrayConfig,
            MsOptions.Create(new SubscriptionOptions { DefaultUpdateIntervalHours = 24 }),
            MsOptions.Create(new DomainSourceOptions { RefreshIntervalHours = 24 }),
            MsOptions.Create(new XrayOptions { UpdateCheckIntervalHours = 12 }),
            MsOptions.Create(new SystemOptions { UpdateCheckIntervalHours = 6 }),
            NullLogger<SettingsService>.Instance);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Без файла настроек: прокси выключен, интервалы — из конфигурации, встроенные домены видны
    /// </summary>
    [Fact]
    public async Task GetAsync_NoFile_ReturnsDefaults()
    {
        var settings = await _sut.GetAsync(Token);

        Assert.Equal("warning", settings.XrayLogLevel);
        Assert.False(settings.UpdateProxy.Enabled);
        Assert.Equal(UpdateProxyDomains.BuiltIn, settings.UpdateProxy.BuiltInDomains);
        Assert.Equal(new UpdateIntervalDto(null, 12, 12, 720), settings.Intervals.XrayUpdateCheck);
        Assert.Equal(new UpdateIntervalDto(null, 24, 24, 168), settings.Intervals.SubscriptionDefault);
    }

    /// <summary>
    ///     Сохранение: домены приводятся к формату Xray, прокси сбрасывается, уровень логов применяется к конфигу Xray
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Valid_SavesAndApplies()
    {
        var saved = await _sut.UpdateAsync(
            new SettingsSaveRequest(
                "Info",
                new UpdateProxySaveRequest(true, "srv-de", ["Example.com", "full:cdn.example.org", "example.com", " "]),
                new UpdateIntervalsSaveRequest(SubscriptionDefaultHours: 6, SystemUpdateCheckHours: 48)),
            Token);

        Assert.Equal("info", saved.XrayLogLevel);
        Assert.Equal(["domain:example.com", "full:cdn.example.org"], saved.UpdateProxy.ExtraDomains);
        Assert.Equal(new UpdateIntervalDto(6, 24, 6, 168), saved.Intervals.SubscriptionDefault);
        Assert.Equal(48, saved.Intervals.SystemUpdateCheck.EffectiveHours);

        var stored = await _settings.GetAsync(Token);
        Assert.True(stored.UpdateProxy.Enabled);
        Assert.Equal("srv-de", stored.UpdateProxy.ServerId);
        await _updateProxy.Received(1).InvalidateAsync(Arg.Any<CancellationToken>());
        await _xrayConfig.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Те же значения повторно — ни конфиг Xray, ни прокси не трогаются
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Unchanged_DoesNotApply()
    {
        await _sut.UpdateAsync(new SettingsSaveRequest("warning", new UpdateProxySaveRequest(false), new UpdateIntervalsSaveRequest()), Token);

        await _updateProxy.DidNotReceive().InvalidateAsync(Arg.Any<CancellationToken>());
        await _xrayConfig.DidNotReceive().ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Ошибки собираются все сразу и ничего не сохраняется
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Invalid_ThrowsWithAllErrors()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => _sut.UpdateAsync(
            new SettingsSaveRequest(
                "verbose",
                new UpdateProxySaveRequest(true, null, ["https://example.com/x"]),
                new UpdateIntervalsSaveRequest(SubscriptionDefaultHours: 500, XrayUpdateCheckHours: 0)),
            Token));

        Assert.Equal(5, ex.Details!.Count);
        Assert.False((await _settings.GetAsync(Token)).UpdateProxy.Enabled);
    }

    /// <summary>
    ///     Несуществующий сервер отклоняется даже при выключенном прокси
    /// </summary>
    [Fact]
    public async Task UpdateAsync_UnknownServer_Throws()
    {
        var ex = await Assert.ThrowsAsync<RequestValidationException>(() => _sut.UpdateAsync(
            new SettingsSaveRequest("warning", new UpdateProxySaveRequest(false, "srv-gone"), new UpdateIntervalsSaveRequest()),
            Token));

        Assert.Contains("srv-gone", ex.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Состояние прокси отдаётся строкой
    /// </summary>
    [Fact]
    public async Task GetUpdateProxyStatusAsync_MapsState()
    {
        _updateProxy.GetStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new UpdateProxyStatus(UpdateProxyState.Unavailable, 10790, null, null, 0, "сервер не выбран"));

        var status = await _sut.GetUpdateProxyStatusAsync(Token);

        Assert.Equal("Unavailable", status.State);
        Assert.Equal("сервер не выбран", status.Reason);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
