using System.Text;
using System.Text.Json;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Разбор вывода <c>xray api bi -json &lt;тег&gt;…</c> (Xray 26.11): JSON-объект на каждый тег по порядку аргументов,
///     <c>{"balancer":{"override":{"target":…},"principleTarget":{"tag":[…]}}}</c>. Неизвестная форма — отказ, не исключение
/// </summary>
public static class XrayBalancerInfoParser
{
    /// <summary>
    ///     Разбирает вывод
    /// </summary>
    /// <param name="output">Стандартный вывод команды</param>
    /// <param name="balancerTags">Теги балансировщиков в порядке аргументов</param>
    /// <param name="activeTags">Выбранный outbound по тегу балансировщика: ручное закрепление, иначе первый из
    ///     выбранных; null — выбора ещё нет</param>
    /// <returns>true, если вывод узнан и объектов столько же, сколько тегов</returns>
    public static bool TryParse(string? output, IReadOnlyList<string> balancerTags, out IReadOnlyDictionary<string, string?> activeTags)
    {
        ArgumentNullException.ThrowIfNull(balancerTags);
        activeTags = new Dictionary<string, string?>();
        if (string.IsNullOrWhiteSpace(output) || balancerTags.Count == 0)
        {
            return false;
        }

        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(output), new JsonReaderOptions { AllowMultipleValues = true });
            var index = 0;
            while (reader.Read())
            {
                if (index >= balancerTags.Count || !TryReadBalancer(ref reader, out var active))
                {
                    return false;
                }

                result[balancerTags[index++]] = active;
            }

            if (index != balancerTags.Count)
            {
                return false;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        activeTags = result;
        return true;
    }

    private static bool TryReadBalancer(ref Utf8JsonReader reader, out string? active)
    {
        active = null;
        using var document = JsonDocument.ParseValue(ref reader);
        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty("balancer", out var balancer)
            || balancer.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (balancer.TryGetProperty("override", out var overrideNode)
            && overrideNode.ValueKind == JsonValueKind.Object
            && overrideNode.TryGetProperty("target", out var target)
            && target.ValueKind == JsonValueKind.String
            && target.GetString() is { Length: > 0 } overridden)
        {
            active = overridden;
            return true;
        }

        if (!balancer.TryGetProperty("principleTarget", out var principle))
        {
            return true;
        }

        if (principle.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (principle.TryGetProperty("tag", out var tags))
        {
            if (tags.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            active = tags.EnumerateArray()
                .Where(tag => tag.ValueKind == JsonValueKind.String)
                .Select(tag => tag.GetString())
                .FirstOrDefault();
        }

        return true;
    }
}
