using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using FlowGate.Tests.Common.Fixtures;
using FlowGate.Tests.Common.Http;

namespace FlowGate.Tests.Common.Keenetic;

/// <summary>
///     Подменный RCI Keenetic с простой моделью интерфейсов, групп <c>object-group fqdn</c> и правил
///     <c>dns-proxy route</c>. Поведение повторяет снятое на роутере (docs/keenetic-findings.md §90–100): удаление группы
///     не удаляет её правила, удаление интерфейса удаляет правила на него, <c>route disable</c> выключает последнее
///     созданное правило. Все команды запоминаются
/// </summary>
public sealed partial class FakeKeeneticRci
{
    private const string ArgumentParseErrorCode = "7405602";

    private readonly Dictionary<string, JsonObject> _interfaces = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FakeDnsGroup> _groups = new(StringComparer.Ordinal);
    private readonly List<FakeDnsRoute> _routes = [];
    private readonly List<string> _commands = [];
    private readonly Lock _sync = new();
    private FakeDnsRoute? _lastCreatedRoute;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="FakeKeeneticRci"/>
    /// </summary>
    public FakeKeeneticRci()
    {
        Handler = new FakeHttpMessageHandler(Respond);
    }

    /// <summary>
    ///     HTTP-обработчик для подключения к клиенту RCI
    /// </summary>
    public FakeHttpMessageHandler Handler { get; }

    /// <summary>
    ///     Команды, на которые нужно ответить ошибкой <c>7405602</c>; можно задать в тесте
    /// </summary>
    public Func<string, bool> FailWhen { get; set; } = _ => false;

    /// <summary>
    ///     Отправленные CLI-команды по порядку, включая <c>system configuration save</c>
    /// </summary>
    public IReadOnlyList<string> Commands
    {
        get
        {
            lock (_sync)
            {
                return _commands.ToList();
            }
        }
    }

    /// <summary>
    ///     Имена существующих интерфейсов
    /// </summary>
    public IReadOnlyList<string> InterfaceNames
    {
        get
        {
            lock (_sync)
            {
                return _interfaces.Keys.ToList();
            }
        }
    }

    /// <summary>
    ///     Группы с элементами
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Groups
    {
        get
        {
            lock (_sync)
            {
                return _groups.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value.Include.ToList(), StringComparer.Ordinal);
            }
        }
    }

    /// <summary>
    ///     Правила в порядке Keenetic
    /// </summary>
    public IReadOnlyList<FakeDnsRoute> Routes
    {
        get
        {
            lock (_sync)
            {
                return _routes.ToList();
            }
        }
    }

    /// <summary>
    ///     Добавляет интерфейс как есть, например из фикстуры
    /// </summary>
    /// <param name="name">Имя интерфейса</param>
    /// <param name="json">Объект интерфейса</param>
    public void AddInterface(string name, string json)
    {
        lock (_sync)
        {
            _interfaces[name] = JsonNode.Parse(json)!.AsObject();
        }
    }

    /// <summary>
    ///     Возвращает объект интерфейса
    /// </summary>
    /// <param name="name">Имя интерфейса</param>
    /// <returns>Копия объекта или null</returns>
    public JsonObject? GetInterface(string name)
    {
        lock (_sync)
        {
            return _interfaces.TryGetValue(name, out var node) ? (JsonObject)node.DeepClone() : null;
        }
    }

    /// <summary>
    ///     Удаляет интерфейс, как будто пользователь удалил его в веб-интерфейсе (правила на него тоже удаляются)
    /// </summary>
    /// <param name="name">Имя интерфейса</param>
    public void RemoveInterface(string name)
    {
        lock (_sync)
        {
            _interfaces.Remove(name);
            _routes.RemoveAll(route => string.Equals(route.Interface, name, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Меняет поле интерфейса, как будто пользователь изменил его в веб-интерфейсе
    /// </summary>
    /// <param name="name">Имя интерфейса</param>
    /// <param name="field">Поле, например <c>state</c></param>
    /// <param name="value">Новое значение</param>
    public void SetInterfaceField(string name, string field, string value)
    {
        lock (_sync)
        {
            _interfaces[name][field] = value;
        }
    }

    /// <summary>
    ///     Возвращает описание группы
    /// </summary>
    /// <param name="name">Группа</param>
    /// <returns>Описание или null, если группы нет</returns>
    public string? GroupDescription(string name)
    {
        lock (_sync)
        {
            return _groups.TryGetValue(name, out var group) ? group.Description : null;
        }
    }

    /// <summary>
    ///     Создаёт группу, как пользователь в веб-интерфейсе
    /// </summary>
    /// <param name="name">Группа</param>
    /// <param name="description">Описание</param>
    /// <param name="entries">Элементы</param>
    public void AddGroup(string name, string description, params string[] entries)
    {
        lock (_sync)
        {
            var group = new FakeDnsGroup { Description = description };
            group.Include.AddRange(entries);
            _groups[name] = group;
        }
    }

    /// <summary>
    ///     Удаляет группу, как пользователь в веб-интерфейсе (правила остаются, как на KeeneticOS 5.2)
    /// </summary>
    /// <param name="name">Группа</param>
    public void RemoveGroup(string name)
    {
        lock (_sync)
        {
            _groups.Remove(name);
        }
    }

    /// <summary>
    ///     Удаляет правило, как пользователь в веб-интерфейсе
    /// </summary>
    /// <param name="group">Группа</param>
    /// <param name="interfaceName">Интерфейс</param>
    public void RemoveRoute(string group, string interfaceName)
    {
        lock (_sync)
        {
            _routes.RemoveAll(route => route.Group == group && route.Interface == interfaceName);
        }
    }

    /// <summary>
    ///     Включает или выключает правило, как пользователь галочкой в веб-интерфейсе
    /// </summary>
    /// <param name="group">Группа</param>
    /// <param name="interfaceName">Интерфейс</param>
    /// <param name="disabled">Выключить</param>
    public void SetRouteDisabled(string group, string interfaceName, bool disabled)
    {
        lock (_sync)
        {
            var index = _routes.FindIndex(route => route.Group == group && route.Interface == interfaceName);
            _routes[index] = _routes[index] with { Disabled = disabled };
        }
    }

    /// <summary>
    ///     Забывает отправленные команды
    /// </summary>
    public void ClearCommands()
    {
        lock (_sync)
        {
            _commands.Clear();
        }
    }

    private HttpResponseMessage Respond(RecordedRequest request)
    {
        var path = request.Uri?.AbsolutePath ?? string.Empty;
        if (request.Method == HttpMethod.Post)
        {
            return FakeHttpMessageHandler.Json(Execute(request.Body ?? "[]"));
        }

        lock (_sync)
        {
            if (path.EndsWith("/show/version", StringComparison.Ordinal))
            {
                return FakeHttpMessageHandler.Json(RciFixtures.ShowVersion);
            }

            if (path.EndsWith("/show/interface", StringComparison.Ordinal))
            {
                var all = new JsonObject();
                foreach (var (name, node) in _interfaces)
                {
                    all[name] = node.DeepClone();
                }

                return FakeHttpMessageHandler.Json(all.ToJsonString());
            }

            if (path.EndsWith("/rci/object-group/fqdn", StringComparison.Ordinal))
            {
                return FakeHttpMessageHandler.Json(GroupsJson());
            }

            if (path.EndsWith("/rci/dns-proxy/route", StringComparison.Ordinal))
            {
                return FakeHttpMessageHandler.Json(RoutesJson());
            }
        }

        return FakeHttpMessageHandler.Json("{}", HttpStatusCode.NotFound);
    }

    private string GroupsJson()
    {
        var all = new JsonObject();
        foreach (var (name, group) in _groups)
        {
            all[name] = new JsonObject
            {
                ["description"] = group.Description,
                ["include"] = new JsonArray(group.Include.Select(entry => (JsonNode)new JsonObject { ["address"] = entry }).ToArray()),
            };
        }

        return all.ToJsonString();
    }

    private string RoutesJson()
    {
        var all = new JsonArray();
        foreach (var route in _routes)
        {
            var node = new JsonObject
            {
                ["group"] = route.Group,
                ["interface"] = route.Interface,
                ["auto"] = route.Auto,
                ["index"] = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(route.Group + route.Interface))),
                ["comment"] = string.Empty,
            };
            if (route.Disabled)
            {
                node["disable"] = true;
            }

            all.Add(node);
        }

        return all.ToJsonString();
    }

    private string Execute(string body)
    {
        using var document = JsonDocument.Parse(body);
        var responses = new JsonArray();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            var command = item.GetProperty("parse").GetString() ?? string.Empty;
            bool ok;
            lock (_sync)
            {
                _commands.Add(command);
                ok = !FailWhen(command) && Apply(command);
            }

            var status = ok
                ? new JsonObject { ["status"] = "message", ["code"] = "0", ["ident"] = "Fake", ["message"] = "ok" }
                : new JsonObject { ["status"] = "error", ["code"] = ArgumentParseErrorCode, ["ident"] = "Command::Base", ["message"] = "argument parse error." };
            responses.Add(new JsonObject { ["parse"] = new JsonObject { ["prompt"] = "(config)", ["status"] = new JsonArray(status) } });
        }

        return responses.ToJsonString();
    }

    private bool Apply(string command)
    {
        var remove = RemoveRegex().Match(command);
        if (remove.Success)
        {
            var name = remove.Groups["name"].Value;
            _routes.RemoveAll(route => string.Equals(route.Interface, name, StringComparison.OrdinalIgnoreCase));
            return _interfaces.Remove(name);
        }

        if (command.StartsWith("object-group fqdn ", StringComparison.Ordinal)
            || command.StartsWith("no object-group fqdn ", StringComparison.Ordinal)
            || command.StartsWith("dns-proxy route", StringComparison.Ordinal)
            || command.StartsWith("no dns-proxy route", StringComparison.Ordinal))
        {
            return ApplyDns(command);
        }

        var match = InterfaceRegex().Match(command);
        if (!match.Success)
        {
            return true;
        }

        var interfaceName = match.Groups["name"].Value;
        var rest = match.Groups["rest"].Value.Trim();
        if (!_interfaces.TryGetValue(interfaceName, out var node))
        {
            if (rest.Length > 0)
            {
                return false;
            }

            _interfaces[interfaceName] = new JsonObject
            {
                ["id"] = interfaceName,
                ["interface-name"] = interfaceName,
                ["type"] = interfaceName.StartsWith("Wireguard", StringComparison.OrdinalIgnoreCase) ? "Wireguard" : "Proxy",
                ["description"] = string.Empty,
                ["link"] = "down",
                ["connected"] = "no",
                ["state"] = "down",
            };
            return true;
        }

        if (rest.StartsWith("description ", StringComparison.Ordinal))
        {
            node["description"] = rest["description ".Length..].Trim('"');
        }
        else if (rest == "up")
        {
            node["state"] = "up";
        }
        else if (rest.StartsWith("ip address ", StringComparison.Ordinal))
        {
            node["address"] = rest.Split(' ')[2];
        }
        else if (rest.StartsWith("proxy upstream ", StringComparison.Ordinal))
        {
            node["remote-endpoint-address"] = rest.Split(' ')[2];
        }
        else if (PeerEndpointRegex().Match(rest) is { Success: true } endpoint)
        {
            var peers = new JsonArray(new JsonObject
            {
                ["public-key"] = endpoint.Groups["key"].Value,
                ["remote-endpoint-address"] = endpoint.Groups["address"].Value,
                ["remote-port"] = int.Parse(endpoint.Groups["port"].Value, CultureInfo.InvariantCulture),
                ["last-handshake"] = int.MaxValue,
                ["online"] = false,
            });
            node["wireguard"] = new JsonObject { ["peer"] = peers };
        }

        return true;
    }

    private bool ApplyDns(string command)
    {
        if (GroupRegex().Match(command) is { Success: true } group)
        {
            return ApplyGroup(group.Groups["no"].Success, group.Groups["name"].Value, group.Groups["rest"].Value.Trim());
        }

        if (command == "dns-proxy route disable")
        {
            if (_lastCreatedRoute is null)
            {
                return false;
            }

            var index = _routes.IndexOf(_lastCreatedRoute);
            _lastCreatedRoute = _lastCreatedRoute with { Disabled = true };
            _routes[index] = _lastCreatedRoute;
            return true;
        }

        if (RouteRegex().Match(command) is not { Success: true } route)
        {
            return false;
        }

        var groupName = route.Groups["group"].Value;
        var interfaceName = route.Groups["iface"].Value;
        if (route.Groups["no"].Success)
        {
            return _routes.RemoveAll(item => item.Group == groupName && item.Interface == interfaceName) > 0;
        }

        if (_routes.Any(item => item.Group == groupName && item.Interface == interfaceName))
        {
            return false;
        }

        _lastCreatedRoute = new FakeDnsRoute(groupName, interfaceName, route.Groups["auto"].Success, false);
        _routes.Add(_lastCreatedRoute);
        return true;
    }

    private bool ApplyGroup(bool negate, string name, string rest)
    {
        if (rest.Length == 0)
        {
            if (negate)
            {
                return _groups.Remove(name);
            }

            _groups.TryAdd(name, new FakeDnsGroup());
            return true;
        }

        if (!_groups.TryGetValue(name, out var existing))
        {
            return false;
        }

        if (!negate && rest.StartsWith("description ", StringComparison.Ordinal))
        {
            existing.Description = rest["description ".Length..].Trim('"');
            return true;
        }

        if (!rest.StartsWith("include ", StringComparison.Ordinal))
        {
            return false;
        }

        var entry = rest["include ".Length..];
        if (negate)
        {
            existing.Include.Remove(entry);
        }
        else if (!existing.Include.Contains(entry))
        {
            existing.Include.Add(entry);
        }

        return true;
    }

    [GeneratedRegex(@"^no interface (?<name>\S+)$")]
    private static partial Regex RemoveRegex();

    [GeneratedRegex(@"^interface (?<name>\S+)(?<rest>.*)$")]
    private static partial Regex InterfaceRegex();

    [GeneratedRegex(@"^wireguard peer (?<key>\S+) endpoint (?<address>[^:\s]+):(?<port>\d+)$")]
    private static partial Regex PeerEndpointRegex();

    [GeneratedRegex(@"^(?<no>no )?object-group fqdn (?<name>\S+)(?<rest>.*)$")]
    private static partial Regex GroupRegex();

    [GeneratedRegex(@"^(?<no>no )?dns-proxy route object-group (?<group>\S+) (?<iface>\S+)(?<auto> auto)?$")]
    private static partial Regex RouteRegex();
}
