using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Раскрытое содержимое подписки
/// </summary>
/// <param name="Json">Xray-JSON, если содержимое — JSON; иначе null</param>
/// <param name="Links">Строки со ссылками на серверы</param>
/// <param name="Metadata">Метаданные провайдера из тела, ключи в нижнем регистре</param>
/// <param name="IgnoredLines">Строки, в которых нет ни ссылки, ни метаданных</param>
public sealed record DecodedSubscriptionContent(
    JsonNode? Json,
    IReadOnlyList<string> Links,
    IReadOnlyDictionary<string, string> Metadata,
    int IgnoredLines);
