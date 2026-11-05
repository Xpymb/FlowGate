using FlowGate.Dal.Implementations.Settings;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace FlowGate.Tests.Unit.Dal;

/// <summary>
///     Тесты <see cref="SettingsProvider"/>: совместимость <c>settings.json</c> между версиями
/// </summary>
public sealed class SettingsProviderTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Файл версии 0.1.2 (только автозапуск и уровень логов) читается: прежние значения сохранены, новые — по умолчанию
    /// </summary>
    [Fact]
    public async Task GetAsync_FileFrom012_KeepsValuesAndDefaultsNewFields()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, SettingsProvider.FileName),
            """{ "xrayAutostart": false, "xrayLogLevel": "info" }""",
            Token);

        var settings = await CreateProvider().GetAsync(Token);

        Assert.False(settings.XrayAutostart);
        Assert.Equal("info", settings.XrayLogLevel);
        Assert.False(settings.UpdateProxy.Enabled);
        Assert.Empty(settings.UpdateProxy.ExtraDomains);
        Assert.Null(settings.Intervals.SubscriptionDefaultHours);
    }

    /// <summary>
    ///     Сохранение не теряет прежние поля и пишет новые; неизвестные поля версия 0.1.2 пропустит
    /// </summary>
    [Fact]
    public async Task UpdateAsync_FileFrom012_WritesOldAndNewFields()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, SettingsProvider.FileName);
        await File.WriteAllTextAsync(path, """{ "xrayAutostart": false, "xrayLogLevel": "info" }""", Token);

        await CreateProvider().UpdateAsync(settings => settings with { XrayLogLevel = "warning" }, Token);
        var text = await File.ReadAllTextAsync(path, Token);

        Assert.Contains("\"xrayAutostart\": false", text, StringComparison.Ordinal);
        Assert.Contains("\"updateProxy\"", text, StringComparison.Ordinal);
        Assert.Contains("\"intervals\"", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Прокси-блок без списка доменов (правка руками) — список пуст, а не null
    /// </summary>
    [Fact]
    public async Task GetAsync_ProxyWithoutDomains_EmptyList()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(
            Path.Combine(_directory, SettingsProvider.FileName),
            """{ "xrayAutostart": true, "xrayLogLevel": "warning", "updateProxy": { "enabled": true, "serverId": "srv" } }""",
            Token);

        var settings = await CreateProvider().GetAsync(Token);

        Assert.True(settings.UpdateProxy.Enabled);
        Assert.Empty(settings.UpdateProxy.ExtraDomains);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SettingsProvider CreateProvider() =>
        new(new JsonFileStoreFactory(MsOptions.Create(new FlowGateOptions { DataDirectory = _directory })));
}
