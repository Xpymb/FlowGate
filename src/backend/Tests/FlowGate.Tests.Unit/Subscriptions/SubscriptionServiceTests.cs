using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using FlowGate.Bll.Implementations.Servers;
using FlowGate.Bll.Implementations.Subscriptions;
using FlowGate.Bll.Interfaces.Subscriptions;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Dal.Implementations.Servers;
using FlowGate.Dal.Implementations.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Subscriptions;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Subscriptions;

/// <summary>
///     Тесты <see cref="SubscriptionService"/> на настоящих хранилищах во временном каталоге; провайдер подменён
/// </summary>
public sealed class SubscriptionServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly FakeTimeProvider _time = new(DateTimeOffset.Parse("2026-11-04T12:00:00Z"));
    private readonly ISubscriptionFetcher _fetcher = Substitute.For<ISubscriptionFetcher>();
    private readonly IXrayConfigService _xrayConfig = Substitute.For<IXrayConfigService>();
    private readonly JobRunner _jobs;
    private readonly ServerProvider _servers;
    private readonly SubscriptionProvider _subscriptions;
    private readonly SubscriptionService _sut;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionServiceTests"/>
    /// </summary>
    public SubscriptionServiceTests()
    {
        var storeFactory = new JsonFileStoreFactory(Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }));
        _servers = new ServerProvider(storeFactory);
        _subscriptions = new SubscriptionProvider(storeFactory);
        _jobs = new JobRunner(_time, NullLogger<JobRunner>.Instance);
        _sut = new SubscriptionService(
            _subscriptions,
            _servers,
            _fetcher,
            new ServerImportParser(),
            _jobs,
            _xrayConfig,
            Microsoft.Extensions.Options.Options.Create(new SubscriptionOptions()),
            _time,
            NullLogger<SubscriptionService>.Instance);

        RespondWith(SubscriptionFixtures.Base64List());
    }

    /// <summary>
    ///     Создание запускает обновление: серверы сохранены, метаданные провайдера и интервал провайдера применены
    /// </summary>
    [Fact]
    public async Task CreateAsync_NewSubscription_RefreshesAndStoresServers()
    {
        var created = await _sut.CreateAsync(new SubscriptionSaveRequest("https://sub.example.com/token"), TestContext.Current.CancellationToken);
        await _jobs.WaitAsync(created.RefreshJobId!.Value, TestContext.Current.CancellationToken);

        var subscription = await _sut.GetAsync(created.Id, TestContext.Current.CancellationToken);

        Assert.Equal(3, subscription.ServerCount);
        Assert.Equal("Мой VPN", subscription.DisplayName);
        Assert.Equal(6, subscription.EffectiveIntervalHours);
        Assert.Equal(1000, subscription.Provider!.TotalBytes);
        Assert.Equal("Ok", subscription.LastRefresh!.Status);
        Assert.Equal(_time.GetUtcNow().AddHours(6), subscription.NextRefreshAt);
        Assert.True(Guid.TryParse(subscription.Hwid, out _));
        await _xrayConfig.Received().ApplyAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Ошибка провайдера записывается в подписку, серверы не трогаются, повтор — через 30 минут
    /// </summary>
    [Fact]
    public async Task RefreshAsync_ProviderFails_RecordsFailureAndKeepsServers()
    {
        var subscription = await CreateRefreshedAsync();
        _fetcher.FetchAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new ExternalServiceUnavailableException("Провайдер sub.example.com ответил HTTP 502"));

        var job = await _sut.RefreshAsync(subscription.Id, TestContext.Current.CancellationToken);
        var finished = await _jobs.WaitAsync(job.Id, TestContext.Current.CancellationToken);
        var after = await _sut.GetAsync(subscription.Id, TestContext.Current.CancellationToken);

        Assert.Equal(JobStatus.Failed, finished!.Status);
        Assert.Equal("Failed", after.LastRefresh!.Status);
        Assert.Contains("502", after.LastRefresh.Error);
        Assert.Equal(3, after.ServerCount);
        Assert.Equal("Мой VPN", after.Provider!.Title);
        Assert.Equal(_time.GetUtcNow().AddMinutes(30), after.NextRefreshAt);
    }

    /// <summary>
    ///     Пустой ответ — ошибка со счётчиками, серверы остаются
    /// </summary>
    [Fact]
    public async Task RefreshAsync_EmptyResponse_FailsAndKeepsServers()
    {
        var subscription = await CreateRefreshedAsync();
        RespondWith("<html>Подписка истекла</html>");

        var job = await _sut.RefreshAsync(subscription.Id, TestContext.Current.CancellationToken);
        await _jobs.WaitAsync(job.Id, TestContext.Current.CancellationToken);
        var after = await _sut.GetAsync(subscription.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Failed", after.LastRefresh!.Status);
        Assert.Equal(3, after.ServerCount);
    }

    /// <summary>
    ///     Планировщик обновляет только подписки, которым пора
    /// </summary>
    [Fact]
    public async Task RefreshDueAsync_OnlyDueSubscriptionsRefreshed()
    {
        await CreateRefreshedAsync();
        _fetcher.ClearReceivedCalls();

        var notDue = await _sut.RefreshDueAsync(TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromHours(6));
        var due = await _sut.RefreshDueAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, notDue);
        Assert.Equal(1, due);
        await _fetcher.Received(1).FetchAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Смена адреса сбрасывает метаданные, удаление подписки удаляет её серверы
    /// </summary>
    [Fact]
    public async Task UpdateAndDelete_ClearMetadataAndRemoveServers()
    {
        var subscription = await CreateRefreshedAsync();

        var updated = await _sut.UpdateAsync(subscription.Id, new SubscriptionSaveRequest("https://other.example.com/x"), TestContext.Current.CancellationToken);
        await _sut.DeleteAsync(subscription.Id, TestContext.Current.CancellationToken);

        Assert.Null(updated.Provider);
        Assert.Equal("other.example.com", updated.DisplayName);
        Assert.Empty(await _servers.GetAllAsync(TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<EntityNotFoundException>(() => _sut.GetAsync(subscription.Id, TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     Некорректные параметры отклоняются
    /// </summary>
    [Theory]
    [InlineData("ftp://sub.example.com", null, null)]
    [InlineData("https://sub.example.com", 0, null)]
    [InlineData("https://sub.example.com", null, "Агент")]
    public async Task CreateAsync_InvalidRequest_ThrowsValidation(string url, int? interval, string? userAgent)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _sut.CreateAsync(
            new SubscriptionSaveRequest(url, UpdateIntervalHours: interval, UserAgent: userAgent),
            TestContext.Current.CancellationToken));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _jobs.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private async Task<SubscriptionDto> CreateRefreshedAsync()
    {
        var created = await _sut.CreateAsync(new SubscriptionSaveRequest("https://sub.example.com/token"), TestContext.Current.CancellationToken);
        await _jobs.WaitAsync(created.RefreshJobId!.Value, TestContext.Current.CancellationToken);
        return created;
    }

    private void RespondWith(string body) =>
        _fetcher.FetchAsync(Arg.Any<Subscription>(), Arg.Any<CancellationToken>())
            .Returns(new SubscriptionDownload(body, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)));
}
