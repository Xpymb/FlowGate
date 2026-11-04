namespace FlowGate.Tests.Common.Http;

/// <summary>
///     Запрос, перехваченный <see cref="FakeHttpMessageHandler"/>
/// </summary>
/// <param name="Method">HTTP-метод</param>
/// <param name="Uri">Полный адрес</param>
/// <param name="Body">Тело запроса или null</param>
public sealed record RecordedRequest(HttpMethod Method, Uri? Uri, string? Body);
