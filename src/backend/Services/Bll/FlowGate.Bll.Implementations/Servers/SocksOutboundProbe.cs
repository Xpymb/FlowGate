using System.Net;

using FlowGate.Bll.Interfaces.Servers;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     HTTP-запрос через socks5-прокси временного Xray; на каждый запрос свой обработчик, чтобы соединения не
///     переиспользовались и задержка включала рукопожатие с сервером
/// </summary>
public sealed class SocksOutboundProbe : IOutboundProbe
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SocksOutboundProbe"/>
    /// </summary>
    /// <param name="timeProvider">Источник времени для замера</param>
    /// <exception cref="ArgumentNullException">Если timeProvider равен null</exception>
    public SocksOutboundProbe(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<TimeSpan> ProbeAsync(int port, Uri url, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        using var handler = new SocketsHttpHandler
        {
            Proxy = new WebProxy($"socks5://127.0.0.1:{port}"),
            UseProxy = true,
            AllowAutoRedirect = false,
            ConnectTimeout = timeout,
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        var started = _timeProvider.GetTimestamp();
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
            return _timeProvider.GetElapsedTime(started);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"нет ответа за {timeout.TotalSeconds:0} с");
        }
    }
}
