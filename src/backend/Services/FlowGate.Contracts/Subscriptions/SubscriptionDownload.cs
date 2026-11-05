namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Скачанный ответ провайдера
/// </summary>
/// <param name="Body">Тело ответа (UTF-8)</param>
/// <param name="Headers">Заголовки ответа и содержимого, ключи без учёта регистра, повторы через запятую</param>
public sealed record SubscriptionDownload(string Body, IReadOnlyDictionary<string, string> Headers);
