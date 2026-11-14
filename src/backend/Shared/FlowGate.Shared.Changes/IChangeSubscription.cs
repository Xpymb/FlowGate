using System.Threading.Channels;

namespace FlowGate.Shared.Changes;

/// <summary>
///     Подписка на <see cref="IChangeFeed"/>
/// </summary>
public interface IChangeSubscription : IDisposable
{
    /// <summary>
    ///     Темы, изменившиеся с момента подписки, в порядке публикации
    /// </summary>
    ChannelReader<string> Topics { get; }
}
