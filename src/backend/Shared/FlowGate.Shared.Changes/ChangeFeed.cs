using System.Threading.Channels;

namespace FlowGate.Shared.Changes;

/// <summary>
///     Шина изменений в памяти: у каждого подписчика своя ограниченная очередь, переполнение вытесняет старые темы
/// </summary>
public sealed class ChangeFeed : IChangeFeed
{
    /// <summary>
    ///     Сколько тем держит очередь одного подписчика
    /// </summary>
    public const int SubscriberCapacity = 64;

    private readonly Lock _sync = new();
    private readonly List<Subscription> _subscriptions = [];

    /// <summary>
    ///     Число активных подписок
    /// </summary>
    public int SubscriberCount
    {
        get
        {
            lock (_sync)
            {
                return _subscriptions.Count;
            }
        }
    }

    /// <inheritdoc />
    public void Publish(string topic)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        Subscription[] subscriptions;
        lock (_sync)
        {
            subscriptions = [.. _subscriptions];
        }

        foreach (var subscription in subscriptions)
        {
            subscription.Channel.Writer.TryWrite(topic);
        }
    }

    /// <inheritdoc />
    public IChangeSubscription Subscribe()
    {
        var subscription = new Subscription(this);
        lock (_sync)
        {
            _subscriptions.Add(subscription);
        }

        return subscription;
    }

    private void Remove(Subscription subscription)
    {
        lock (_sync)
        {
            _subscriptions.Remove(subscription);
        }
    }

    private sealed class Subscription : IChangeSubscription
    {
        private readonly ChangeFeed _feed;
        private int _disposed;

        public Subscription(ChangeFeed feed)
        {
            _feed = feed;
            Channel = System.Threading.Channels.Channel.CreateBounded<string>(new BoundedChannelOptions(SubscriberCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
        }

        public Channel<string> Channel { get; }

        public ChannelReader<string> Topics => Channel.Reader;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }

            _feed.Remove(this);
            Channel.Writer.TryComplete();
        }
    }
}
