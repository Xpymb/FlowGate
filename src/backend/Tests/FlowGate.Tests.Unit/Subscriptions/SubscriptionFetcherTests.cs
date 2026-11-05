using System.Net;
using System.Text;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using FlowGate.Bll.Implementations.Subscriptions;
using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Exceptions;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Keenetic;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Unit.Subscriptions;

/// <summary>
///     Тесты <see cref="SubscriptionFetcher"/>: заголовки, перенаправления, отказы HWID, лимит размера
/// </summary>
public class SubscriptionFetcherTests
{
    private readonly IKeeneticSystemService _keenetic = Substitute.For<IKeeneticSystemService>();

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SubscriptionFetcherTests"/>
    /// </summary>
    public SubscriptionFetcherTests()
    {
        _keenetic.GetSystemInfoAsync(Arg.Any<CancellationToken>()).Returns(
            new KeeneticSystemInfoDto("Hopper SE (KN-3812)", "KN-3812", "5.02.B.2.0-2", "5.2 Beta 2", "aarch64", true, [], []));
    }

    /// <summary>
    ///     Отправляются User-Agent и HWID; сведения об устройстве — только при включённой галочке
    /// </summary>
    [Fact]
    public async Task FetchAsync_DefaultSubscription_SendsUserAgentAndHwidOnly()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("body"));
        var sut = CreateFetcher(new CapturingHandler(handler, request => captured = request));

        var download = await sut.FetchAsync(Subscription(), TestContext.Current.CancellationToken);

        Assert.Equal("body", download.Body);
        Assert.StartsWith("FlowGate/", captured!.Headers.UserAgent.ToString());
        Assert.Equal("hwid-1", captured.Headers.GetValues("x-hwid").Single());
        Assert.False(captured.Headers.Contains("X-Device-Model"));
    }

    /// <summary>
    ///     Свой User-Agent и сведения об устройстве из show version
    /// </summary>
    [Fact]
    public async Task FetchAsync_DeviceInfoAndCustomAgent_SendsDeviceHeaders()
    {
        HttpRequestMessage? captured = null;
        var sut = CreateFetcher(new CapturingHandler(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("ok")), request => captured = request));

        await sut.FetchAsync(Subscription() with { UserAgent = "v2rayN/7.0", SendDeviceInfo = true, SendHwid = false }, TestContext.Current.CancellationToken);

        Assert.Equal("v2rayN/7.0", captured!.Headers.UserAgent.ToString());
        Assert.False(captured.Headers.Contains("x-hwid"));
        Assert.Equal("KeeneticOS", captured.Headers.GetValues("X-Device-OS").Single());
        Assert.Equal("KN-3812", captured.Headers.GetValues("X-Device-Model").Single());
        Assert.Equal("5.2 Beta 2", captured.Headers.GetValues("X-Ver-OS").Single());
    }

    /// <summary>
    ///     Перенаправление на другой origin выполняется без сведений об устройстве
    /// </summary>
    [Fact]
    public async Task FetchAsync_CrossOriginRedirect_DropsDeviceHeaders()
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHttpMessageHandler(request => request.Uri!.Host == "sub.example.com"
            ? Redirect("https://cdn.example.org/real")
            : FakeHttpMessageHandler.Json("final"));
        var sut = CreateFetcher(new CapturingHandler(handler, requests.Add));

        var download = await sut.FetchAsync(Subscription() with { SendDeviceInfo = true }, TestContext.Current.CancellationToken);

        Assert.Equal("final", download.Body);
        Assert.Equal(2, requests.Count);
        Assert.True(requests[0].Headers.Contains("X-Device-Model"));
        Assert.False(requests[1].Headers.Contains("X-Device-Model"));
        Assert.True(requests[1].Headers.Contains("x-hwid"));
    }

    /// <summary>
    ///     Перенаправление с HTTPS на HTTP запрещено
    /// </summary>
    [Fact]
    public async Task FetchAsync_RedirectToHttp_Throws()
    {
        var sut = CreateFetcher(new FakeHttpMessageHandler(_ => Redirect("http://sub.example.com/plain")));

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => sut.FetchAsync(Subscription(), TestContext.Current.CancellationToken));

        Assert.Contains("HTTPS на HTTP", exception.Message);
    }

    /// <summary>
    ///     Отказ по HWID распознаётся по флагу даже при 200
    /// </summary>
    [Fact]
    public async Task FetchAsync_HwidLimitFlag_ThrowsHwidError()
    {
        var sut = CreateFetcher(new FakeHttpMessageHandler(_ =>
        {
            var response = FakeHttpMessageHandler.Json(string.Empty);
            response.Headers.Add("x-hwid-max-devices-reached", "true");
            return response;
        }));

        var exception = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(
            () => sut.FetchAsync(Subscription(), TestContext.Current.CancellationToken));

        Assert.Contains("HWID", exception.Message);
    }

    /// <summary>
    ///     Ответ больше лимита и HTTP-ошибка отклоняются, адрес подписки в сообщение не попадает
    /// </summary>
    [Fact]
    public async Task FetchAsync_TooLargeOrHttpError_ThrowsWithoutUrl()
    {
        var large = CreateFetcher(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Bytes(new byte[4096])), maxBytes: 1024);
        var error = CreateFetcher(new FakeHttpMessageHandler(_ => FakeHttpMessageHandler.Json("no", HttpStatusCode.Forbidden)));

        var tooLarge = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => large.FetchAsync(Subscription(), TestContext.Current.CancellationToken));
        var forbidden = await Assert.ThrowsAsync<ExternalServiceUnavailableException>(() => error.FetchAsync(Subscription(), TestContext.Current.CancellationToken));

        Assert.Contains("больше", tooLarge.Message);
        Assert.Contains("403", forbidden.Message);
        Assert.DoesNotContain("token", forbidden.Message);
    }

    private SubscriptionFetcher CreateFetcher(HttpMessageHandler handler, int maxBytes = 5 * 1024 * 1024)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(SubscriptionFetcher.HttpClientName).Returns(_ => new HttpClient(handler, disposeHandler: false));

        var services = new ServiceCollection().AddSingleton(_keenetic).BuildServiceProvider();

        return new SubscriptionFetcher(
            factory,
            services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new SubscriptionOptions { MaxResponseBytes = maxBytes }),
            NullLogger<SubscriptionFetcher>.Instance);
    }

    private static Subscription Subscription() =>
        new("sub1", null, "https://sub.example.com/api/sub/token123", true, null, null, true, "hwid-1", false, null, null, DateTimeOffset.UnixEpoch);

    private static HttpResponseMessage Redirect(string location)
    {
        var response = new HttpResponseMessage(HttpStatusCode.Found) { Content = new StringContent(string.Empty, Encoding.UTF8) };
        response.Headers.Location = new Uri(location);
        return response;
    }

    private sealed class CapturingHandler(HttpMessageHandler inner, Action<HttpRequestMessage> capture) : DelegatingHandler(inner)
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            capture(request);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
