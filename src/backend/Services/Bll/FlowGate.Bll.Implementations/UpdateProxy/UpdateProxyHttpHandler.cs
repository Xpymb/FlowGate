using System.Net;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.UpdateProxy;

/// <summary>
///     Первичный обработчик HTTP-клиентов обновлений. Внутри <see cref="UpdateProxyScope"/> запрос идёт путём операции;
///     вне её — через прокси, если он включён и доступен, иначе напрямую. Сбой самого прокси на GET/HEAD в режиме
///     <see cref="UpdateVia.Auto"/> — один повтор напрямую
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
            return scope.ProxyUri is { } scopeProxy
                ? await SendProxiedAsync(request, scopeProxy, retryDirect: scope.Via == UpdateVia.Auto, cancellationToken)
                : await _direct.SendAsync(request, cancellationToken);
        }

        var proxyUri = await _proxy.ResolveAsync(UpdateVia.Auto, cancellationToken);
        return proxyUri is null
            ? await _direct.SendAsync(request, cancellationToken)
            : await SendProxiedAsync(request, proxyUri, retryDirect: true, cancellationToken);
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
        Uri proxyUri,
        bool retryDirect,
        CancellationToken cancellationToken)
    {
        try
        {
            return await Proxied(proxyUri).SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex) when (retryDirect && IsRetriable(request))
        {
            _logger.LogWarning(
                "Request to {Host} via update proxy failed ({Error}), retrying directly",
                request.RequestUri?.Host,
                ex.HttpRequestError);
            return await _direct.SendAsync(request, cancellationToken);
        }
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
}
