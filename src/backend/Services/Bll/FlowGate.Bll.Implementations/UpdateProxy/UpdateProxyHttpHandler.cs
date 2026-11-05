using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.UpdateProxy;

/// <summary>
///     Первичный обработчик HTTP-клиентов обновлений. Внутри <see cref="UpdateProxyScope"/> запрос идёт путём и арендой
///     операции; вне её — через прокси, если он включён и доступен (аренда держится, пока читается тело ответа), иначе
///     напрямую. Сбой самого прокси на GET/HEAD в режиме <see cref="UpdateVia.Auto"/> — один повтор напрямую
/// </summary>
public sealed class UpdateProxyHttpHandler : HttpMessageHandler
{
    private readonly IUpdateProxyService _proxy;
    private readonly HttpMessageInvoker _direct;
    private readonly Func<Uri, HttpMessageHandler> _createProxied;
    private readonly ILogger<UpdateProxyHttpHandler> _logger;
    private readonly Lock _sync = new();
    private HttpMessageInvoker? _proxied;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyHttpHandler"/>
    /// </summary>
    /// <param name="proxy">Прокси обновлений</param>
    /// <param name="direct">Обработчик прямых запросов</param>
    /// <param name="createProxied">Создаёт обработчик, который ходит через прокси по этому адресу; вызывается один раз</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public UpdateProxyHttpHandler(
        IUpdateProxyService proxy,
        HttpMessageHandler direct,
        Func<Uri, HttpMessageHandler> createProxied,
        ILogger<UpdateProxyHttpHandler> logger)
    {
        _proxy = proxy ?? throw new ArgumentNullException(nameof(proxy));
        ArgumentNullException.ThrowIfNull(direct);
        _direct = new HttpMessageInvoker(direct, disposeHandler: true);
        _createProxied = createProxied ?? throw new ArgumentNullException(nameof(createProxied));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    ///     Создаёт обработчик с двумя одинаково настроенными <see cref="SocketsHttpHandler"/>: прямым и через прокси
    /// </summary>
    /// <param name="services">Провайдер сервисов</param>
    /// <param name="createHandler">Создаёт настроенный обработчик клиента (перенаправления, распаковка и т.д.)</param>
    /// <returns>Обработчик</returns>
    public static UpdateProxyHttpHandler Create(IServiceProvider services, Func<SocketsHttpHandler> createHandler)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(createHandler);

        var direct = createHandler();
        direct.UseProxy = false;

        return new UpdateProxyHttpHandler(
            services.GetRequiredService<IUpdateProxyService>(),
            direct,
            proxyUri =>
            {
                var proxied = createHandler();
                proxied.UseProxy = true;
                proxied.Proxy = new WebProxy(proxyUri);
                return proxied;
            },
            services.GetRequiredService<ILogger<UpdateProxyHttpHandler>>());
    }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (UpdateProxyScope.Current is { } scope)
        {
            return scope.Lease is { } scopeLease
                ? await SendProxiedAsync(request, scopeLease, ownsLease: false, retryDirect: scope.Via == UpdateVia.Auto, cancellationToken)
                : await _direct.SendAsync(request, cancellationToken);
        }

        var lease = await _proxy.AcquireAsync(UpdateVia.Auto, cancellationToken);
        return lease is null
            ? await _direct.SendAsync(request, cancellationToken)
            : await SendProxiedAsync(request, lease, ownsLease: true, retryDirect: true, cancellationToken);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _direct.Dispose();
            lock (_sync)
            {
                _proxied?.Dispose();
                _proxied = null;
            }
        }

        base.Dispose(disposing);
    }

    private async Task<HttpResponseMessage> SendProxiedAsync(
        HttpRequestMessage request,
        UpdateProxyLease lease,
        bool ownsLease,
        bool retryDirect,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await Proxied(lease.ProxyUri).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (retryDirect && IsRetriable(request))
        {
            if (ownsLease)
            {
                lease.Dispose();
            }

            _logger.LogWarning(
                "Запрос к {Host} через прокси обновлений не удался ({Error}), повтор напрямую",
                request.RequestUri?.Host,
                ex.HttpRequestError);
            return await _direct.SendAsync(request, cancellationToken);
        }
        catch
        {
            if (ownsLease)
            {
                lease.Dispose();
            }

            throw;
        }

        if (ownsLease)
        {
            response.Content = new LeasedContent(response.Content, lease);
        }

        return response;
    }

    private HttpMessageInvoker Proxied(Uri proxyUri)
    {
        lock (_sync)
        {
            return _proxied ??= new HttpMessageInvoker(_createProxied(proxyUri), disposeHandler: true);
        }
    }

    private static bool IsRetriable(HttpRequestMessage request) =>
        request.Method == HttpMethod.Get || request.Method == HttpMethod.Head;

    private sealed class LeasedContent : HttpContent
    {
        private readonly HttpContent _inner;
        private readonly UpdateProxyLease _lease;

        public LeasedContent(HttpContent inner, UpdateProxyLease lease)
        {
            _inner = inner;
            _lease = lease;
            foreach (var header in inner.Headers)
            {
                Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            await SerializeToStreamAsync(stream, context, CancellationToken.None);

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context, CancellationToken cancellationToken)
        {
            try
            {
                await _inner.CopyToAsync(stream, cancellationToken);
            }
            finally
            {
                _lease.Dispose();
            }
        }

        protected override async Task<Stream> CreateContentReadStreamAsync() =>
            await CreateContentReadStreamAsync(CancellationToken.None);

        protected override async Task<Stream> CreateContentReadStreamAsync(CancellationToken cancellationToken) =>
            new LeasedStream(await _inner.ReadAsStreamAsync(cancellationToken), _lease);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _lease.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class LeasedStream : Stream
    {
        private readonly Stream _inner;
        private readonly UpdateProxyLease _lease;

        public LeasedStream(Stream inner, UpdateProxyLease lease)
        {
            _inner = inner;
            _lease = lease;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => Released(_inner.Read(buffer, offset, count));

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            Released(await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken));

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            Released(await _inner.ReadAsync(buffer, cancellationToken));

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _lease.Dispose();
            }

            base.Dispose(disposing);
        }

        private int Released(int read)
        {
            if (read == 0)
            {
                _lease.Dispose();
            }

            return read;
        }
    }
}
