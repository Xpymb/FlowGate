using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.Settings;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
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
    ///     Без файла настроек: прокси выключен, интервалы — из конфигурации
    /// </summary>
    [Fact]
    public async Task GetAsync_NoFile_ReturnsDefaults()
    {
        var settings = await _sut.GetAsync(Token);

        Assert.Equal("warning", settings.XrayLogLevel);
        Assert.False(settings.UpdateProxy.Enabled);
        Assert.Equal(new UpdateIntervalDto(null, 12, 12, 720), settings.Intervals.XrayUpdateCheck);
        Assert.Equal(new UpdateIntervalDto(null, 24, 24, 168), settings.Intervals.SubscriptionDefault);
    }

    /// <summary>
    ///     Сохранение: уровень логов и сервер прокси применяются к конфигу Xray одним вызовом
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Valid_SavesAndApplies()
    {
        var saved = await _sut.UpdateAsync(
            new SettingsSaveRequest(
                "Info",
                new UpdateProxySaveRequest(true, " srv-de "),
                new UpdateIntervalsSaveRequest(SubscriptionDefaultHours: 6, SystemUpdateCheckHours: 48)),
            Token);

        Assert.Equal("info", saved.XrayLogLevel);
        Assert.Equal("srv-de", saved.UpdateProxy.ServerId);
        Assert.Equal(new UpdateIntervalDto(6, 24, 6, 168), saved.Intervals.SubscriptionDefault);
        Assert.Equal(48, saved.Intervals.SystemUpdateCheck.EffectiveHours);

        var stored = await _settings.GetAsync(Token);
        Assert.True(stored.UpdateProxy.Enabled);
        Assert.Equal("srv-de", stored.UpdateProxy.ServerId);
        await _xrayConfig.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Те же значения повторно — конфиг Xray не трогается
    /// </summary>
    [Fact]
    public async Task UpdateAsync_Unchanged_DoesNotApply()
    {
        await _sut.UpdateAsync(new SettingsSaveRequest("warning", new UpdateProxySaveRequest(false), new UpdateIntervalsSaveRequest()), Token);

        await _xrayConfig.DidNotReceive().ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Сменился только сервер прокси — inbound в основном Xray пересобирается
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ProxyServerChanged_Applies()
    {
        await _settings.UpdateAsync(settings => settings with { UpdateProxy = new UpdateProxySettings { Enabled = true } }, Token);

        await _sut.UpdateAsync(new SettingsSaveRequest("warning", new UpdateProxySaveRequest(true, "srv-de"), new UpdateIntervalsSaveRequest()), Token);

        await _xrayConfig.Received(1).ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Переключение прокси при том же сервере — Xray не перезапускается: inbound в конфиге есть в обоих случаях
    /// </summary>
    [Fact]
    public async Task UpdateAsync_OnlyEnabledToggled_DoesNotApply()
    {
        await _settings.UpdateAsync(settings => settings with { UpdateProxy = new UpdateProxySettings { Enabled = true, ServerId = "srv-de" } }, Token);

        await _sut.UpdateAsync(new SettingsSaveRequest("warning", new UpdateProxySaveRequest(false, "srv-de"), new UpdateIntervalsSaveRequest()), Token);

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
                new UpdateProxySaveRequest(true, null),
                new UpdateIntervalsSaveRequest(SubscriptionDefaultHours: 500, XrayUpdateCheckHours: 0)),
            Token));

        Assert.Equal(4, ex.Details!.Count);
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
            .Returns(new UpdateProxyStatus(UpdateProxyState.Unavailable, 10790, "сервер не выбран"));

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
