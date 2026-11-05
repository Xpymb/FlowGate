using System.Text.RegularExpressions;

namespace FlowGate.Contracts.DomainLists;

/// <summary>
///     Разбор текста списка в формате v2fly domain-list-community с расширением FlowGate: строка-адрес или подсеть.
///     Поведение повторяет keenetic-geosite-sync (<c>src/domainsList.ts</c>): <c>#</c> — комментарий, токены через
///     пробел, <c>@attr</c> и <c>@attr=N</c> — атрибуты, <c>include:имя</c> — вложенный список
/// </summary>
public static partial class V2flyListParser
{
    /// <summary>
    ///     Проверяет имя списка: как у v2fly, <c>[a-z0-9!-]</c>, плюс <c>.</c> и <c>_</c>
    /// </summary>
    /// <param name="name">Имя</param>
    /// <returns>true, если имя допустимо</returns>
    public static bool IsValidListName(string? name) => name is { Length: > 0 and <= 64 } && ListNameRegex().IsMatch(name);

    /// <summary>
    ///     Проверяет фильтр атрибута: <c>ads</c> или <c>!ads</c>
    /// </summary>
    /// <param name="filter">Фильтр</param>
    /// <returns>true, если фильтр допустим</returns>
    public static bool IsValidAttributeFilter(string? filter) => filter is { Length: > 0 and <= 64 } && AttributeFilterRegex().IsMatch(filter);

    /// <summary>
    ///     Разбирает текст списка
    /// </summary>
    /// <param name="text">Текст</param>
    /// <returns>Правила и ошибки по строкам</returns>
    public static V2flyParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var rules = new List<V2flyRule>();
        var errors = new List<V2flyParseError>();
        var lines = text.TrimStart('﻿').Split('\n');

        for (var index = 0; index < lines.Length; index++)
        {
            var line = StripComment(lines[index]);
            if (line.Length == 0)
            {
                continue;
            }

            var tokens = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            try
            {
                rules.Add(ParseRule(tokens));
            }
            catch (FormatException ex)
            {
                errors.Add(new V2flyParseError(index + 1, ex.Message));
            }
        }

        return new V2flyParseResult(rules, errors);
    }

    private static string StripComment(string line)
    {
        var hash = line.IndexOf('#', StringComparison.Ordinal);
        return (hash >= 0 ? line[..hash] : line).Trim();
    }

    private static V2flyRule ParseRule(string[] tokens)
    {
        var attributes = new List<string>();
        var payload = new List<string>();
        foreach (var token in tokens)
        {
            if (token.StartsWith('@'))
            {
                attributes.Add(ParseAttribute(token));
            }
            else
            {
                payload.Add(token);
            }
        }

        if (payload.Count == 0)
        {
            throw new FormatException("пустое правило");
        }

        if (payload.Count > 2 || (payload.Count == 2 && !payload[0].EndsWith(':') && payload[0] != "include"))
        {
            throw new FormatException($"лишние значения: «{string.Join(' ', payload)}»");
        }

        var head = payload[0];
        var next = payload.Count > 1 ? payload[1] : null;

        if (head == "include" || head.StartsWith("include:", StringComparison.Ordinal))
        {
            var target = (head == "include" ? next : head["include:".Length..] is { Length: > 0 } inline ? inline : next)?.ToLowerInvariant();
            return IsValidListName(target)
                ? new V2flyRule(V2flyRuleKind.Include, target!, attributes)
                : throw new FormatException($"include без корректного имени списка: «{string.Join(' ', payload)}»");
        }

        var colon = head.IndexOf(':', StringComparison.Ordinal);
        var prefix = colon > 0 ? head[..colon] : null;
        if (prefix is not null and not ("domain" or "full" or "keyword" or "regexp"))
        {
            return ParseValue(V2flyRuleKind.Domain, head, attributes);
        }

        var value = prefix is null ? head : head[(colon + 1)..] is { Length: > 0 } rest ? rest : next;
        if (string.IsNullOrEmpty(value))
        {
            throw new FormatException($"нет значения: «{head}»");
        }

        return prefix switch
        {
            "full" => ParseValue(V2flyRuleKind.Full, value, attributes),
            "keyword" => new V2flyRule(V2flyRuleKind.Keyword, value.ToLowerInvariant(), attributes),
            "regexp" => new V2flyRule(V2flyRuleKind.Regexp, value, attributes),
            _ => ParseValue(V2flyRuleKind.Domain, value, attributes),
        };
    }

    private static V2flyRule ParseValue(V2flyRuleKind kind, string value, List<string> attributes)
    {
        if (!ListEntries.TryNormalize(value, out var entry, out var entryKind))
        {
            throw new FormatException($"не домен и не адрес: «{value}»");
        }

        return entryKind == ListEntryKind.Domain
            ? new V2flyRule(kind, entry, attributes)
            : new V2flyRule(V2flyRuleKind.Address, entry, attributes);
    }

    private static string ParseAttribute(string token)
    {
        var body = token[1..];
        var name = body.Split('=', 2)[0].ToLowerInvariant();
        if (name.Length == 0 || !AttributeNameRegex().IsMatch(name))
        {
            throw new FormatException($"некорректный атрибут «{token}»");
        }

        if (body.Contains('=', StringComparison.Ordinal) && !int.TryParse(body[(body.IndexOf('=') + 1)..], out _))
        {
            throw new FormatException($"значение атрибута «{token}» должно быть числом");
        }

        return name;
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9!._-]*$")]
    private static partial Regex ListNameRegex();

    [GeneratedRegex("^!?[a-z0-9][a-z0-9!._-]*$")]
    private static partial Regex AttributeFilterRegex();

    [GeneratedRegex("^[a-z0-9!._-]+$")]
    private static partial Regex AttributeNameRegex();
}
