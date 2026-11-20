using System.Net;
using System.Text;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.SharedContracts.Dto.Settings;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Unit.UpdateProxy;

/// <summary>
///     Тесты <see cref="UpdateProxyHttpHandler"/>: прямой запрос, запрос через прокси, повтор напрямую, путь области операции
/// </summary>
public sealed class UpdateProxyHttpHandlerTests : IDisposable
{
    private static readonly Uri ProxyUri = new("http://127.0.0.1:10790/");

    private readonly IUpdateProxyService _proxy = Substitute.For<IUpdateProxyService>();
    private readonly FakeHttpMessageHandler _direct = new(_ => Text("direct"));
    private readonly FakeHttpMessageHandler _proxied = new(_ => Text("proxied"));
    private readonly List<Uri> _proxiedCreatedFor = [];
    private readonly HttpClient _client;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyHttpHandlerTests"/>
    /// </summary>
    public UpdateProxyHttpHandlerTests()
    {
        var handler = new UpdateProxyHttpHandler(
            _proxy,
            _direct,
            uri =>
            {
                _proxiedCreatedFor.Add(uri);
                return _proxied;
            },
            NullLogger<UpdateProxyHttpHandler>.Instance);
        _client = new HttpClient(handler);
        _proxy.ResolveAsync(Arg.Any<UpdateVia>(), Arg.Any<CancellationToken>()).Returns(ProxyUri);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    ///     Прокси выключен или недоступен — запрос идёт напрямую
    /// </summary>
    [Fact]
    public async Task SendAsync_NoProxy_UsesDirect()
    {
        _proxy.ResolveAsync(Arg.Any<UpdateVia>(), Arg.Any<CancellationToken>()).Returns((Uri?)null);

        var body = await _client.GetStringAsync(new Uri("https://api.github.com/repos"), Token);

        Assert.Equal("direct", body);
        Assert.Empty(_proxied.Requests);
    }

    /// <summary>
    ///     Через прокси: обработчик создаётся один раз для адреса прокси
    /// </summary>
    [Fact]
    public async Task SendAsync_Proxy_UsesProxyHandlerOnce()
    {
        var first = await _client.GetStringAsync(new Uri("https://api.github.com/a"), Token);
        var second = await _client.GetStringAsync(new Uri("https://api.github.com/b"), Token);

        Assert.Equal("proxied", first);
        Assert.Equal("proxied", second);
        Assert.Equal([ProxyUri], _proxiedCreatedFor);
        Assert.Empty(_direct.Requests);
    }

    /// <summary>
    ///     Сбой самого прокси на GET — один повтор напрямую
    /// </summary>
    [Fact]
    public async Task SendAsync_ProxyFailsOnGet_RetriesDirect()
    {
        _proxied.Responder = _ => throw new HttpRequestException(HttpRequestError.ProxyTunnelError, "tunnel failed");

        var body = await _client.GetStringAsync(new Uri("https://api.github.com/repos"), Token);

        Assert.Equal("direct", body);
    }

    /// <summary>
    ///     POST через упавший прокси не повторяется: ошибка уходит вызывающему
    /// </summary>
    [Fact]
    public async Task SendAsync_ProxyFailsOnPost_Throws()
    {
        _proxied.Responder = _ => throw new HttpRequestException(HttpRequestError.ConnectionError, "refused");

        await Assert.ThrowsAsync<HttpRequestException>(
            () => _client.PostAsync(new Uri("https://example.com/"), new StringContent("x"), Token));

        Assert.Empty(_direct.Requests);
    }

    /// <summary>
    ///     Внутри области операции запросы идут её путём: прокси заново не выбирается
    /// </summary>
    [Fact]
    public async Task SendAsync_InProxyScope_UsesScopeProxy()
    {
        using (UpdateProxyScope.Enter(UpdateVia.Proxy, ProxyUri))
        {
            Assert.Equal("proxied", await _client.GetStringAsync(new Uri("https://api.github.com/a"), Token));
            Assert.Equal("proxied", await _client.GetStringAsync(new Uri("https://api.github.com/b"), Token));
        }

        await _proxy.DidNotReceive().ResolveAsync(Arg.Any<UpdateVia>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Область без прокси (Direct или прокси недоступен в Auto) — напрямую
    /// </summary>
    [Fact]
    public async Task SendAsync_InDirectScope_UsesDirect()
    {
        using var scope = UpdateProxyScope.Enter(UpdateVia.Direct, null);

        Assert.Equal("direct", await _client.GetStringAsync(new Uri("https://api.github.com/a"), Token));
        await _proxy.DidNotReceive().ResolveAsync(Arg.Any<UpdateVia>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    ///     Принудительно через прокси: сбой прокси не переводит запрос на прямую загрузку
    /// </summary>
    [Fact]
    public async Task SendAsync_ForcedProxyFails_DoesNotRetryDirect()
    {
        _proxied.Responder = _ => throw new HttpRequestException(HttpRequestError.ProxyTunnelError, "tunnel failed");
        using var scope = UpdateProxyScope.Enter(UpdateVia.Proxy, ProxyUri);

        await Assert.ThrowsAsync<HttpRequestException>(() => _client.GetStringAsync(new Uri("https://api.github.com/a"), Token));

        Assert.Empty(_direct.Requests);
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private static HttpResponseMessage Text(string text) =>
        new(HttpStatusCode.OK) { Content = new StringContent(text, Encoding.UTF8, "text/plain") };
}
