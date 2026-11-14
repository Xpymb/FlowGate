using Microsoft.Extensions.Logging.Abstractions;

using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Changes;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;
using FlowGate.Tests.Unit.Dal;

namespace FlowGate.Tests.Unit.Changes;

/// <summary>
///     Тесты <see cref="ChangeFeed"/> и публикации изменений хранилищем и фоновыми задачами
/// </summary>
public sealed class ChangeFeedTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));
    private readonly ChangeFeed _sut = new();

    /// <summary>
    ///     Тема доходит до каждого подписчика
    /// </summary>
    [Fact]
    public void Publish_TwoSubscribers_BothReceiveTopic()
    {
        using var first = _sut.Subscribe();
        using var second = _sut.Subscribe();

        _sut.Publish("servers");

        Assert.Equal(["servers"], Drain(first));
        Assert.Equal(["servers"], Drain(second));
    }

    /// <summary>
    ///     Освобождённая подписка снимается и больше ничего не получает
    /// </summary>
    [Fact]
    public void Dispose_Subscription_StopsDeliveryAndCompletesReader()
    {
        var subscription = _sut.Subscribe();

        subscription.Dispose();
        _sut.Publish("servers");

        Assert.Equal(0, _sut.SubscriberCount);
        Assert.True(subscription.Topics.Completion.IsCompleted);
        Assert.Empty(Drain(subscription));
    }

    /// <summary>
    ///     Переполненная очередь вытесняет старые темы и не блокирует публикацию
    /// </summary>
    [Fact]
    public void Publish_MoreThanCapacity_KeepsLatestTopics()
    {
        using var subscription = _sut.Subscribe();

        for (var index = 0; index < ChangeFeed.SubscriberCapacity + 10; index++)
        {
            _sut.Publish($"topic-{index}");
        }

        var topics = Drain(subscription);
        Assert.Equal(ChangeFeed.SubscriberCapacity, topics.Count);
        Assert.Equal($"topic-{ChangeFeed.SubscriberCapacity + 9}", topics[^1]);
    }

    /// <summary>
    ///     Тема файла — имя без расширения
    /// </summary>
    [Fact]
    public void ForFile_JsonFileName_ReturnsNameWithoutExtension()
    {
        Assert.Equal("dns-routes", ChangeTopics.ForFile("dns-routes.json"));
    }

    /// <summary>
    ///     Запись и изменение через хранилище фабрики публикуют тему файла
    /// </summary>
    [Fact]
    public async Task JsonFileStore_WriteAndUpdate_PublishesFileTopic()
    {
        using var subscription = _sut.Subscribe();
        var factory = new JsonFileStoreFactory(
            Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }),
            _sut);
        var store = factory.Create("dns-routes.json", TestDocumentJsonContext.Default.TestDocument, () => TestDocument.Default);

        await store.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Empty(Drain(subscription));

        await store.WriteAsync(new TestDocument(1, "a"), TestContext.Current.CancellationToken);
        await store.UpdateAsync(document => document with { Counter = 2 }, TestContext.Current.CancellationToken);

        Assert.Equal(["dns-routes", "dns-routes"], Drain(subscription));
    }

    /// <summary>
    ///     Задача публикует тему jobs на запуск, шаг и завершение
    /// </summary>
    [Fact]
    public async Task JobRunner_JobLifecycle_PublishesJobsTopic()
    {
        using var subscription = _sut.Subscribe();
        using var runner = new JobRunner(TimeProvider.System, NullLogger<JobRunner>.Instance, _sut);

        var job = runner.Start("kind", "Задача", (context, _) =>
        {
            context.Report("шаг");
            return Task.CompletedTask;
        });
        await runner.WaitAsync(job.Id, TestContext.Current.CancellationToken);
        await WaitForCountAsync(subscription, 3);

        Assert.All(Drain(subscription), topic => Assert.Equal(ChangeTopics.Jobs, topic));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static List<string> Drain(IChangeSubscription subscription)
    {
        var topics = new List<string>();
        while (subscription.Topics.TryRead(out var topic))
        {
            topics.Add(topic);
        }

        return topics;
    }

    private static async Task WaitForCountAsync(IChangeSubscription subscription, int count)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (subscription.Topics.Count < count)
        {
            await Task.Delay(10, timeout.Token);
        }
    }
}
