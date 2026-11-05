using FlowGate.Contracts.Servers;

namespace FlowGate.Contracts.Subscriptions;

/// <summary>
///     Сопоставление серверов подписки с новым ответом провайдера.
///     Серверы сопоставляются по <see cref="ServerImportKey"/>: ID сохраняется, даже если провайдер сменил пароль.
///     Если часть узлов ответа не разобрана, пропавшие серверы не удаляются (ответ мог быть неполным)
/// </summary>
public static class SubscriptionSync
{
    /// <summary>
    ///     Вычисляет новый набор серверов подписки
    /// </summary>
    /// <param name="subscriptionId">Подписка</param>
    /// <param name="existing">Текущие серверы этой подписки</param>
    /// <param name="batch">Разобранный ответ</param>
    /// <param name="now">Текущее время</param>
    /// <param name="maxServers">Предельное число серверов подписки</param>
    /// <returns>Новый набор и результат; при статусе <see cref="SubscriptionRefreshStatus.Failed"/> набор равен текущему</returns>
    /// <exception cref="ArgumentNullException">Если existing или batch равны null</exception>
    public static SubscriptionSyncOutcome Apply(
        string subscriptionId,
        IReadOnlyList<Server> existing,
        ServerImportBatch batch,
        DateTimeOffset now,
        int maxServers)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(batch);

        var warnings = batch.Warnings.ToList();
        var accepted = new List<(string Key, ParsedServer Parsed)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var duplicates = 0;
        foreach (var parsed in batch.Servers)
        {
            var key = ServerImportKey.Compute(parsed.Outbound);
            if (seen.Add(key))
            {
                accepted.Add((key, parsed));
            }
            else
            {
                duplicates++;
            }
        }

        if (duplicates > 0)
        {
            warnings.Add($"Пропущено дубликатов: {duplicates}");
        }

        if (accepted.Count == 0)
        {
            var error = batch.Received == 0
                ? "В ответе провайдера нет серверов"
                : "Ни один сервер из ответа провайдера не удалось разобрать";
            return new SubscriptionSyncOutcome(
                existing,
                new SubscriptionRefreshResult(now, SubscriptionRefreshStatus.Failed, error, batch.Received, 0, batch.Rejected.Count, 0, 0, 0, 0, warnings));
        }

        var byKey = existing
            .GroupBy(server => server.ImportKey, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var result = new List<Server>();
        int added = 0, updated = 0;
        foreach (var (key, parsed) in accepted)
        {
            if (byKey.TryGetValue(key, out var old))
            {
                var changed = old.Name != parsed.Name || !System.Text.Json.Nodes.JsonNode.DeepEquals(old.Outbound, parsed.Outbound);
                result.Add(changed ? old with { Name = parsed.Name, Outbound = parsed.Outbound, UpdatedAt = now } : old);
                updated += changed ? 1 : 0;
            }
            else
            {
                result.Add(new Server(StableId(subscriptionId, key), parsed.Name, parsed.Outbound, ServerSourceKind.Subscription, subscriptionId, key, now, now));
                added++;
            }
        }

        var partial = batch.Rejected.Count > 0;
        var missing = existing.Where(server => !seen.Contains(server.ImportKey)).ToList();
        var retained = partial ? missing : [];
        result.AddRange(retained);

        if (result.Count > maxServers)
        {
            return new SubscriptionSyncOutcome(
                existing,
                new SubscriptionRefreshResult(
                    now,
                    SubscriptionRefreshStatus.Failed,
                    $"В подписке больше {maxServers} серверов",
                    batch.Received,
                    accepted.Count,
                    batch.Rejected.Count,
                    0,
                    0,
                    0,
                    0,
                    warnings));
        }

        if (partial)
        {
            warnings.Add($"Не разобрано узлов: {batch.Rejected.Count}. Серверы, пропавшие из ответа, сохранены и могут быть устаревшими");
        }

        return new SubscriptionSyncOutcome(
            result,
            new SubscriptionRefreshResult(
                now,
                partial ? SubscriptionRefreshStatus.Partial : SubscriptionRefreshStatus.Ok,
                null,
                batch.Received,
                accepted.Count,
                batch.Rejected.Count,
                added,
                updated,
                partial ? 0 : missing.Count,
                retained.Count,
                warnings));
    }

    private static string StableId(string subscriptionId, string importKey) => $"sub-{subscriptionId}-{importKey[..12]}";
}
