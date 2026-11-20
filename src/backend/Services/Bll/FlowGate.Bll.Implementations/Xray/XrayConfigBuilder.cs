using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

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

        var routing = new JsonObject
        {
            ["domainStrategy"] = "AsIs",
            ["rules"] = new JsonArray(model.Rules.Select(BuildRule).ToArray<JsonNode?>()),
        };

        if (model.Balancers.Count > 0)
        {
            routing["balancers"] = new JsonArray(model.Balancers.Select(BuildBalancer).ToArray<JsonNode?>());
        }

        var config = new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = model.LogLevel },
            ["inbounds"] = new JsonArray(model.Inbounds.Select(BuildInbound).ToArray<JsonNode?>()),
            ["outbounds"] = new JsonArray(BuildOutbounds(model).ToArray<JsonNode?>()),
            ["routing"] = routing,
        };

        if (model.Observatory is { } observatory)
        {
            config["burstObservatory"] = BuildObservatory(observatory);
        }

        if (model.Api is { } api)
        {
            config["api"] = new JsonObject
            {
                ["tag"] = XrayApi.Tag,
                ["listen"] = api.Listen,
                ["services"] = new JsonArray(api.Services.Select(service => (JsonNode?)service).ToArray()),
            };
        }

        return config;
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

    private static JsonObject BuildRule(XrayRoutingRule rule)
    {
        var node = new JsonObject
        {
            ["type"] = "field",
            ["inboundTag"] = new JsonArray(rule.InboundTags.Select(tag => (JsonNode?)tag).ToArray()),
        };

        if (rule.Domains is { Count: > 0 } domains)
        {
            node["domain"] = new JsonArray(domains.Select(domain => (JsonNode?)domain).ToArray());
        }

        if (rule.BalancerTag is not null)
        {
            node["balancerTag"] = rule.BalancerTag;
        }
        else
        {
            node["outboundTag"] = rule.OutboundTag;
        }

        return node;
    }

    private static JsonObject BuildBalancer(XrayBalancer balancer)
    {
        var costs = balancer.Costs
            .Select(cost => (JsonNode?)new JsonObject
            {
                ["regexp"] = true,
                ["match"] = "^" + Regex.Escape(cost.Key) + "$",
                ["value"] = cost.Value,
            })
            .ToArray();

        var settings = new JsonObject { ["expected"] = 1, ["costs"] = new JsonArray(costs) };
        if (balancer.Tolerance > 0)
        {
            settings["tolerance"] = balancer.Tolerance;
        }

        return new JsonObject
        {
            ["tag"] = balancer.Tag,
            ["selector"] = new JsonArray(balancer.Selector.Select(tag => (JsonNode?)tag).ToArray()),
            ["fallbackTag"] = balancer.FallbackTag,
            ["strategy"] = new JsonObject { ["type"] = "leastLoad", ["settings"] = settings },
        };
    }

    private static JsonObject BuildObservatory(XrayObservatory observatory) =>
        new()
        {
            ["subjectSelector"] = new JsonArray(observatory.SubjectSelector.Select(tag => (JsonNode?)tag).ToArray()),
            ["pingConfig"] = new JsonObject
            {
                ["destination"] = observatory.ProbeUrl,
                ["interval"] = observatory.Interval,
                ["sampling"] = observatory.Sampling,
                ["timeout"] = observatory.Timeout,
            },
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

        if (model.Api is not null && (inboundTags.Contains(XrayApi.Tag) || outboundTags.Contains(XrayApi.Tag)))
        {
            throw Invalid($"тег «{XrayApi.Tag}» зарезервирован для API");
        }

        outboundTags.Add(XrayConfigModel.DirectTag);
        outboundTags.Add(XrayConfigModel.BlockTag);

        var balancerTags = EnsureUnique(model.Balancers.Select(balancer => balancer.Tag), "балансировщика");
        foreach (var balancer in model.Balancers)
        {
            if (balancer.Selector.Count == 0)
            {
                throw Invalid($"у балансировщика «{balancer.Tag}» нет кандидатов");
            }

            var unknown = balancer.Selector.Append(balancer.FallbackTag).FirstOrDefault(tag => !outboundTags.Contains(tag));
            if (unknown is not null)
            {
                throw Invalid($"балансировщик «{balancer.Tag}» ссылается на несуществующий outbound «{unknown}»");
            }
        }

        foreach (var rule in model.Rules)
        {
            var unknownInbound = rule.InboundTags.FirstOrDefault(tag => !inboundTags.Contains(tag));
            if (unknownInbound is not null)
            {
                throw Invalid($"правило ссылается на несуществующий inbound «{unknownInbound}»");
            }

            if ((rule.OutboundTag is null) == (rule.BalancerTag is null))
            {
                throw Invalid("у правила должен быть ровно один из outbound и балансировщика");
            }

            if (rule.OutboundTag is not null && !outboundTags.Contains(rule.OutboundTag))
            {
                throw Invalid($"правило ссылается на несуществующий outbound «{rule.OutboundTag}»");
            }

            if (rule.BalancerTag is not null && !balancerTags.Contains(rule.BalancerTag))
            {
                throw Invalid($"правило ссылается на несуществующий балансировщик «{rule.BalancerTag}»");
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
