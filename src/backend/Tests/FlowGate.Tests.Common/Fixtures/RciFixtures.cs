using System.Text.Json;

namespace FlowGate.Tests.Common.Fixtures;

/// <summary>
///     Ответы RCI, снятые с Keenetic Hopper SE на KeeneticOS 5.2 Beta 2 (этап 0, docs/keenetic-findings.md)
/// </summary>
public static class RciFixtures
{
    /// <summary>
    ///     <c>GET /rci/show/version</c>
    /// </summary>
    public static string ShowVersion => Load("show-version.json");

    /// <summary>
    ///     Ответ на 3 команды: создание группы fgspike-test, description, include example.com — все успешны
    /// </summary>
    public static string ParseGroupCreated => Load("parse-group-created.json");

    /// <summary>
    ///     Ответ на 3 команды, где вторая — неизвестная (<c>7405600 no such command</c>), а первая и третья выполнены
    /// </summary>
    public static string ParseMixedError => Load("parse-mixed-error.json");

    /// <summary>
    ///     Ответ на одну неизвестную команду (<c>7405600</c>)
    /// </summary>
    public static string ParseNoSuchCommand => Load("parse-no-such-command.json");

    /// <summary>
    ///     Ответ на 1001-й include в группу (<c>101583600 too many elements</c>)
    /// </summary>
    public static string ParseTooManyElements => Load("parse-too-many-elements.json");

    /// <summary>
    ///     Объект интерфейса Proxy9 из <c>GET /rci/show/interface</c>: socks5 на 127.0.0.1:10899, xray не слушает
    /// </summary>
    public static string ShowInterfaceProxy => Load("show-interface-proxy.json");

    /// <summary>
    ///     Объект интерфейса Wireguard9 из <c>GET /rci/show/interface</c>: peer на 127.0.0.1:10898 без рукопожатия
    ///     (<c>last-handshake</c> = 2147483647)
    /// </summary>
    public static string ShowInterfaceWireguard => Load("show-interface-wireguard.json");

    /// <summary>
    ///     <c>GET /rci/object-group/fqdn</c> с группой fgspike: домены, IPv4/IPv6-адреса и подсети, в том числе
    ///     ненормализованная <c>91.108.56.1/22</c> (Keenetic хранит как есть)
    /// </summary>
    public static string ObjectGroupFqdn => Load("object-group-fqdn.json");

    /// <summary>
    ///     <c>GET /rci/dns-proxy/route</c>: выключенное правило fgspike и включённое fgspike2 на Proxy9
    /// </summary>
    public static string DnsProxyRoute => Load("dns-proxy-route.json");

    /// <summary>
    ///     Разбирает фикстуру в <see cref="JsonElement"/>
    /// </summary>
    /// <param name="json">Текст фикстуры</param>
    /// <returns>Корневой элемент</returns>
    public static JsonElement ToElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string Load(string fileName)
    {
        var resourceName = $"FlowGate.Tests.Common.Fixtures.Rci.{fileName}";
        using var stream = typeof(RciFixtures).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Фикстура {resourceName} не найдена");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
