using System.Net;
using System.Text;

namespace FlowGate.Tests.Common.Http;

/// <summary>
///     Подменный HTTP-обработчик: запоминает запросы и отвечает заданной функцией
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<RecordedRequest> _requests = [];
    private readonly Lock _sync = new();

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FakeHttpMessageHandler"/>
    /// </summary>
    /// <param name="responder">Функция, формирующая ответ на запрос</param>
    /// <exception cref="ArgumentNullException">Если responder равен null</exception>
    public FakeHttpMessageHandler(Func<RecordedRequest, HttpResponseMessage> responder)
    {
        Responder = responder ?? throw new ArgumentNullException(nameof(responder));
    }

    /// <summary>
    ///     Функция, формирующая ответ; можно заменить между запросами
    /// </summary>
    public Func<RecordedRequest, HttpResponseMessage> Responder { get; set; }

    /// <summary>
    ///     Перехваченные запросы в порядке поступления
    /// </summary>
    public IReadOnlyList<RecordedRequest> Requests
    {
        get
        {
            lock (_sync)
            {
                return _requests.ToList();
            }
        }
    }

    /// <summary>
    ///     Создаёт обработчик, который на любой запрос отвечает JSON
    /// </summary>
    /// <param name="json">Тело ответа</param>
    /// <param name="statusCode">HTTP-статус</param>
    /// <returns>Обработчик</returns>
    public static FakeHttpMessageHandler RespondJson(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(_ => Json(json, statusCode));

    /// <summary>
    ///     Создаёт обработчик, который на любой запрос бросает исключение
    /// </summary>
    /// <param name="exception">Исключение</param>
    /// <returns>Обработчик</returns>
    public static FakeHttpMessageHandler Throw(Exception exception) => new(_ => throw exception);

    /// <summary>
    ///     Создаёт JSON-ответ
    /// </summary>
    /// <param name="json">Тело ответа</param>
    /// <param name="statusCode">HTTP-статус</param>
    /// <returns>Ответ</returns>
    public static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri, body);

        lock (_sync)
        {
            _requests.Add(recorded);
        }

        return Responder(recorded);
    }
}
