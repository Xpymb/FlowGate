using System.Text;
using System.Text.Json;

using FlowGate.Exceptions;
using FlowGate.Shared.Keenetic.Models;
using FlowGate.Shared.Keenetic.Serialization;

namespace FlowGate.Shared.Keenetic;

/// <summary>
///     Клиент RCI Keenetic поверх <see cref="HttpClient"/>
/// </summary>
public sealed class KeeneticRciClient : IKeeneticRciClient
{
    /// <summary>
    ///     Команда сохранения конфигурации
    /// </summary>
    public const string SaveConfigurationCommand = "system configuration save";

    private readonly HttpClient _httpClient;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticRciClient"/>
    /// </summary>
    /// <param name="httpClient">HTTP-клиент с базовым адресом RCI</param>
    /// <exception cref="ArgumentNullException">Если httpClient равен null</exception>
    public KeeneticRciClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <inheritdoc />
    public async Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var relativePath = path.TrimStart('/');
        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, relativePath),
            relativePath,
            cancellationToken);

        return await ReadJsonAsync(response, relativePath, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RciBatchResult> ExecuteAsync(IReadOnlyList<string> commands, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(commands);

        if (commands.Count == 0)
        {
            return RciBatchResult.Empty;
        }

        var body = JsonSerializer.Serialize(
            commands.Select(command => new RciParseRequest(command)).ToList(),
            KeeneticRciJsonContext.Default.ListRciParseRequest);

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Post, string.Empty)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            },
            $"parse x{commands.Count}",
            cancellationToken);

        var json = await ReadJsonAsync(response, "parse", cancellationToken);
        return ParseBatch(commands, json);
    }

    /// <inheritdoc />
    public async Task SaveConfigurationAsync(CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync([SaveConfigurationCommand], cancellationToken);
        result.EnsureSuccess();
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> createRequest,
        string description,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            using var request = createRequest();
            response = await _httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new KeeneticUnavailableException($"RCI недоступен ({description}): {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new KeeneticUnavailableException($"RCI не ответил вовремя ({description})", ex);
        }

        if (!response.IsSuccessStatusCode)
        {
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new KeeneticUnavailableException($"RCI ответил HTTP {status} ({description})");
        }

        return response;
    }

    private static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response,
        string description,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new KeeneticUnavailableException($"RCI вернул некорректный JSON ({description})", ex);
        }
    }

    private static RciBatchResult ParseBatch(IReadOnlyList<string> commands, JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Array || json.GetArrayLength() != commands.Count)
        {
            throw new KeeneticUnavailableException(
                $"RCI вернул неожиданный ответ на пакет из {commands.Count} команд(ы)");
        }

        var results = json.EnumerateArray()
            .Select((item, index) => ParseCommand(commands[index], item))
            .ToList();

        return new RciBatchResult(results);
    }

    private static RciCommandResult ParseCommand(string command, JsonElement item)
    {
        if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("parse", out var payload))
        {
            throw new KeeneticUnavailableException($"RCI вернул ответ без поля parse для команды «{command}»");
        }

        var prompt = payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("prompt", out var promptElement)
            && promptElement.ValueKind == JsonValueKind.String
                ? promptElement.GetString()
                : null;

        var statuses = payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty("status", out var statusElement)
            && statusElement.ValueKind == JsonValueKind.Array
                ? statusElement.EnumerateArray().Select(ParseStatus).ToList()
                : [];

        return new RciCommandResult(command, prompt, statuses, payload);
    }

    private static RciStatus ParseStatus(JsonElement status) =>
        new(
            ReadString(status, "status"),
            ReadString(status, "code"),
            ReadString(status, "ident"),
            ReadString(status, "message"));

    private static string ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => value.GetRawText(),
            }
            : string.Empty;
}
