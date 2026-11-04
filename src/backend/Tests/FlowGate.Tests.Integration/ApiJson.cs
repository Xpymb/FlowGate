using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FlowGate.Tests.Integration;

/// <summary>
///     Помощники для вызова API в интеграционных тестах
/// </summary>
public static class ApiJson
{
    /// <summary>
    ///     GET с проверкой статуса 200
    /// </summary>
    /// <param name="client">Клиент</param>
    /// <param name="url">Адрес</param>
    /// <returns>Тело ответа</returns>
    public static async Task<JsonElement> GetAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync(response);
    }

    /// <summary>
    ///     POST JSON с проверкой статуса
    /// </summary>
    /// <param name="client">Клиент</param>
    /// <param name="url">Адрес</param>
    /// <param name="body">Тело запроса</param>
    /// <param name="expected">Ожидаемый статус</param>
    /// <returns>Тело ответа</returns>
    public static async Task<JsonElement> PostAsync(HttpClient client, string url, object body, HttpStatusCode expected)
    {
        using var response = await client.PostAsJsonAsync(url, body, TestContext.Current.CancellationToken);
        Assert.Equal(expected, response.StatusCode);
        return await ReadAsync(response);
    }

    /// <summary>
    ///     Читает тело ответа как JSON
    /// </summary>
    /// <param name="response">Ответ</param>
    /// <returns>Корневой элемент или пустой объект для пустого тела</returns>
    public static async Task<JsonElement> ReadAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(text.Length == 0 ? "{}" : text);
        return document.RootElement.Clone();
    }

    /// <summary>
    ///     Ждёт завершения фоновой задачи
    /// </summary>
    /// <param name="client">Клиент</param>
    /// <param name="jobId">Идентификатор задачи</param>
    /// <returns>Завершённая задача</returns>
    /// <exception cref="TimeoutException">Если задача не завершилась за 10 с</exception>
    public static async Task<JsonElement> WaitForJobAsync(HttpClient client, string jobId)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var job = await GetAsync(client, $"/api/jobs/{jobId}");
            if (job.GetProperty("status").GetString() != "Running")
            {
                return job;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Задача {jobId} не завершилась за 10 с");
            }

            await Task.Delay(20);
        }
    }
}
