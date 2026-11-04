using System.Text.Json.Nodes;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Сборка <c>xray.json</c> из <see cref="XrayConfigModel"/>
/// </summary>
public sealed class XrayConfigBuilder : IXrayConfigBuilder
{
    /// <inheritdoc />
    public JsonObject Build(XrayConfigModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);

        return new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = model.LogLevel },
            ["inbounds"] = new JsonArray(model.Inbounds.Select(BuildInbound).ToArray<JsonNode?>()),
            ["outbounds"] = new JsonArray(BuildOutbounds(model).ToArray<JsonNode?>()),
            ["routing"] = new JsonObject
            {
                ["domainStrategy"] = "AsIs",
                ["rules"] = new JsonArray(model.Rules.Select(BuildRule).ToArray<JsonNode?>()),
            },
        };
    }

    private static JsonObject BuildInbound(XrayInbound inbound)
    {
        var node = new JsonObject
        {
            ["tag"] = inbound.Tag,
            ["listen"] = inbound.Listen,
            ["port"] = inbound.Port,
            ["protocol"] = inbound.Protocol,
            ["settings"] = inbound.Settings?.DeepClone() ?? new JsonObject(),
        };

        if (inbound.Sniffing)
        {
            node["sniffing"] = new JsonObject
            {
                ["enabled"] = true,
                ["destOverride"] = new JsonArray("http", "tls", "quic"),
            };
        }

        return node;
    }

    private static IEnumerable<JsonObject> BuildOutbounds(XrayConfigModel model)
    {
        yield return new JsonObject { ["tag"] = XrayConfigModel.DirectTag, ["protocol"] = "freedom" };
        yield return new JsonObject { ["tag"] = XrayConfigModel.BlockTag, ["protocol"] = "blackhole" };

        foreach (var outbound in model.Outbounds)
        {
            var node = (JsonObject)outbound.Definition.DeepClone();
            node["tag"] = outbound.Tag;
            yield return node;
        }
    }

    private static JsonObject BuildRule(XrayRoutingRule rule) =>
        new()
        {
            ["type"] = "field",
            ["inboundTag"] = new JsonArray(rule.InboundTags.Select(tag => (JsonNode?)tag).ToArray()),
            ["outboundTag"] = rule.OutboundTag,
        };

    private static void Validate(XrayConfigModel model)
    {
        if (!XrayConfigModel.LogLevels.Contains(model.LogLevel))
        {
            throw Invalid($"неизвестный уровень логирования «{model.LogLevel}»");
        }

        var inboundTags = EnsureUnique(model.Inbounds.Select(inbound => inbound.Tag), "inbound");
        var outboundTags = EnsureUnique(model.Outbounds.Select(outbound => outbound.Tag), "outbound");

        var reserved = outboundTags.FirstOrDefault(tag => tag is XrayConfigModel.DirectTag or XrayConfigModel.BlockTag);
        if (reserved is not null)
        {
            throw Invalid($"тег outbound «{reserved}» зарезервирован");
        }

        outboundTags.Add(XrayConfigModel.DirectTag);
        outboundTags.Add(XrayConfigModel.BlockTag);

        foreach (var rule in model.Rules)
        {
            var unknownInbound = rule.InboundTags.FirstOrDefault(tag => !inboundTags.Contains(tag));
            if (unknownInbound is not null)
            {
                throw Invalid($"правило ссылается на несуществующий inbound «{unknownInbound}»");
            }

            if (!outboundTags.Contains(rule.OutboundTag))
            {
                throw Invalid($"правило ссылается на несуществующий outbound «{rule.OutboundTag}»");
            }
        }

        var routed = model.Rules.SelectMany(rule => rule.InboundTags).ToHashSet(StringComparer.Ordinal);
        var unrouted = inboundTags.FirstOrDefault(tag => !routed.Contains(tag));
        if (unrouted is not null)
        {
            throw Invalid($"для inbound «{unrouted}» нет правила маршрутизации");
        }
    }

    private static HashSet<string> EnsureUnique(IEnumerable<string> tags, string kind)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw Invalid($"пустой тег {kind}");
            }

            if (!unique.Add(tag))
            {
                throw Invalid($"тег {kind} «{tag}» повторяется");
            }
        }

        return unique;
    }

    private static XrayOperationException Invalid(string reason) =>
        new($"Некорректная модель конфигурации Xray: {reason}");
}
