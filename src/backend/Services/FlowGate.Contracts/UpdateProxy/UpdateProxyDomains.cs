using System.Text.RegularExpressions;

namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Домены, которые прокси обновлений отправляет через выбранный сервер, в формате правил Xray
/// </summary>
public static partial class UpdateProxyDomains
{
    /// <summary>
    ///     Наибольшее число своих доменов пользователя
    /// </summary>
    public const int MaxExtraDomains = 200;

    private const string DomainPrefix = "domain:";
    private const string FullPrefix = "full:";

    /// <summary>
    ///     Встроенные домены: API и файлы GitHub (релизы Xray, geosite/geoip, tarball репозиториев), feed FlowGate
    ///     на GitHub Pages и Entware
    /// </summary>
    public static IReadOnlyList<string> BuiltIn { get; } =
    [
        "domain:github.com",
        "domain:githubusercontent.com",
        "domain:github.io",
        "full:bin.entware.net",
    ];

    /// <summary>
    ///     Приводит домен пользователя к формату Xray: голый хост становится <c>domain:хост</c>, регистр — нижний
    /// </summary>
    /// <param name="value">Строка пользователя: <c>example.com</c>, <c>domain:example.com</c>, <c>full:cdn.example.com</c></param>
    /// <returns>Домен в формате Xray или null, если строка не похожа на домен</returns>
    public static string? Normalize(string? value)
    {
        var text = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var prefix = DomainPrefix;
        if (text.StartsWith(FullPrefix, StringComparison.Ordinal))
        {
            prefix = FullPrefix;
            text = text[FullPrefix.Length..];
        }
        else if (text.StartsWith(DomainPrefix, StringComparison.Ordinal))
        {
            text = text[DomainPrefix.Length..];
        }

        text = text.TrimEnd('.');
        return text.Length <= 253 && HostPattern().IsMatch(text) ? prefix + text : null;
    }

    /// <summary>
    ///     Собирает полный список: встроенные, хосты подписок и прямых ссылок источников (<c>full:</c>), свои домены.
    ///     Повторы убираются, порядок стабилен, чтобы одинаковый набор давал одинаковый конфиг
    /// </summary>
    /// <param name="urls">Адреса, чьи хосты нужно добавить; из URL берётся только хост</param>
    /// <param name="extra">Свои домены пользователя в формате Xray</param>
    /// <returns>Домены для правила маршрутизации</returns>
    public static IReadOnlyList<string> Collect(IEnumerable<string> urls, IEnumerable<string> extra)
    {
        ArgumentNullException.ThrowIfNull(urls);
        ArgumentNullException.ThrowIfNull(extra);

        var hosts = urls
            .Select(url => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.HostNameType == UriHostNameType.Dns ? uri.IdnHost : null)
            .Select(host => host is null ? null : Normalize(FullPrefix + host));

        return BuiltIn
            .Concat(hosts.OfType<string>().Order(StringComparer.Ordinal))
            .Concat(extra.Select(Normalize).OfType<string>())
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)(\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex HostPattern();
}
