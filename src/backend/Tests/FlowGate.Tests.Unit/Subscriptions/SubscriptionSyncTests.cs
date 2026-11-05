using FlowGate.Bll.Implementations.Servers;
using FlowGate.Contracts.Servers;
using FlowGate.Contracts.Subscriptions;
using FlowGate.Tests.Common.Fixtures;

namespace FlowGate.Tests.Unit.Subscriptions;

/// <summary>
///     Тесты <see cref="SubscriptionSync"/> и <see cref="ServerImportKey"/>: стабильные ID, частичные ответы, лимиты
/// </summary>
public class SubscriptionSyncTests
{
    private const string SubscriptionId = "sub1";

    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-04T12:00:00Z");

    private readonly ServerImportParser _parser = new();

    /// <summary>
    ///     Первое обновление добавляет все серверы со стабильными ID
    /// </summary>
    [Fact]
    public void Apply_FirstRefresh_AddsAll()
    {
        var outcome = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList()), Now, 500);

        Assert.Equal(SubscriptionRefreshStatus.Ok, outcome.Result.Status);
        Assert.Equal(3, outcome.Result.Added);
        Assert.All(outcome.Servers, server =>
        {
            Assert.StartsWith("sub-sub1-", server.Id);
            Assert.Equal(ServerSourceKind.Subscription, server.SourceKind);
            Assert.Equal(SubscriptionId, server.SubscriptionId);
        });
    }

    /// <summary>
    ///     Смена пароля провайдером сохраняет ID сервера, сервер помечается изменённым
    /// </summary>
    [Fact]
    public void Apply_PasswordRotation_KeepsIdsAndUpdates()
    {
        var first = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList("secret1")), Now, 500);

        var second = SubscriptionSync.Apply(SubscriptionId, first.Servers, _parser.Parse(SubscriptionFixtures.PlainList("secret2")), Now.AddHours(1), 500);

        Assert.Equal(first.Servers.Select(server => server.Id), second.Servers.Select(server => server.Id));
        Assert.Equal(1, second.Result.Updated);
        Assert.Equal(0, second.Result.Added);
        var trojan = second.Servers.Single(server => server.Name == "DE");
        Assert.Equal("secret2", trojan.Outbound["settings"]!["password"]!.GetValue<string>());
        Assert.Equal(Now.AddHours(1), trojan.UpdatedAt);
        Assert.Equal(Now, second.Servers.Single(server => server.Name == "NL").UpdatedAt);
    }

    /// <summary>
    ///     Полный ответ без сервера удаляет его
    /// </summary>
    [Fact]
    public void Apply_FullResponseWithoutServer_RemovesIt()
    {
        var first = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList()), Now, 500);
        var reduced = string.Join('\n', SubscriptionFixtures.PlainList().Split('\n').Where(line => !line.Contains("hy2://")));

        var second = SubscriptionSync.Apply(SubscriptionId, first.Servers, _parser.Parse(reduced), Now, 500);

        Assert.Equal(SubscriptionRefreshStatus.Ok, second.Result.Status);
        Assert.Equal(1, second.Result.Removed);
        Assert.Equal(2, second.Servers.Count);
    }

    /// <summary>
    ///     Частичный ответ (есть отклонённые узлы) не удаляет пропавшие серверы
    /// </summary>
    [Fact]
    public void Apply_PartialResponse_RetainsMissing()
    {
        var first = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList()), Now, 500);
        var partial = "vless://00000000-0000-0000-0000-000000000001@nl.example.net:443?type=tcp&security=reality&sni=www.example.com&pbk=PUBLICKEY1&sid=ab#NL\nvless://broken";

        var second = SubscriptionSync.Apply(SubscriptionId, first.Servers, _parser.Parse(partial), Now, 500);

        Assert.Equal(SubscriptionRefreshStatus.Partial, second.Result.Status);
        Assert.Equal(2, second.Result.Retained);
        Assert.Equal(0, second.Result.Removed);
        Assert.Equal(3, second.Servers.Count);
        Assert.Contains(second.Result.Warnings, warning => warning.Contains("сохранены"));
    }

    /// <summary>
    ///     Ни одного разобранного сервера — ошибка, набор не меняется
    /// </summary>
    [Fact]
    public void Apply_NothingAccepted_FailsAndKeepsExisting()
    {
        var first = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList()), Now, 500);

        var second = SubscriptionSync.Apply(SubscriptionId, first.Servers, _parser.Parse("vless://broken\n<html>ошибка</html>"), Now, 500);

        Assert.Equal(SubscriptionRefreshStatus.Failed, second.Result.Status);
        Assert.Same(first.Servers, second.Servers);
    }

    /// <summary>
    ///     Дубликаты внутри ответа сливаются, превышение лимита — ошибка
    /// </summary>
    [Fact]
    public void Apply_DuplicatesAndLimit_Handled()
    {
        var link = "trojan://p@de.example.net:443?sni=de.example.net#DE";
        var duplicated = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse($"{link}\n{link}"), Now, 500);
        var overLimit = SubscriptionSync.Apply(SubscriptionId, [], _parser.Parse(SubscriptionFixtures.PlainList()), Now, 2);

        Assert.Single(duplicated.Servers);
        Assert.Contains(duplicated.Result.Warnings, warning => warning.Contains("дубликатов: 1"));
        Assert.Equal(SubscriptionRefreshStatus.Failed, overLimit.Result.Status);
        Assert.Empty(overLimit.Servers);
    }

    /// <summary>
    ///     Один сервер ссылкой (плоский settings) и в Xray-JSON (vnext) даёт одинаковый ключ: смена формата подписки не меняет ID
    /// </summary>
    [Fact]
    public void ServerImportKey_LinkAndXrayJsonForms_AreEqual()
    {
        var fromLink = _parser.Parse(
            "vless://id@nl.example.net:443?type=xhttp&security=reality&sni=www.example.com&pbk=KEY&sid=ab&path=%2F&mode=packet-up#NL").Servers[0];
        var fromJson = _parser.Parse(
            """
            [{ "remarks": "NL", "outbounds": [
              { "tag": "proxy", "protocol": "vless",
                "settings": { "vnext": [ { "address": "nl.example.net", "port": 443, "users": [ { "id": "id", "encryption": "none" } ] } ] },
                "streamSettings": { "network": "xhttp", "security": "reality",
                  "realitySettings": { "serverName": "www.example.com", "publicKey": "KEY", "shortId": "ab" },
                  "xhttpSettings": { "path": "/", "mode": "packet-up" } } },
              { "tag": "DIRECT", "protocol": "freedom" } ] }]
            """).Servers[0];

        Assert.Equal(ServerImportKey.Compute(fromLink.Outbound), ServerImportKey.Compute(fromJson.Outbound));
    }

    /// <summary>
    ///     Ключ не зависит от учётных данных, но зависит от адреса и порта
    /// </summary>
    [Fact]
    public void ServerImportKey_CredentialsIgnoredAddressCounts()
    {
        var a = _parser.Parse("trojan://one@de.example.net:443#A").Servers[0].Outbound;
        var b = _parser.Parse("trojan://two@DE.example.net:443#B").Servers[0].Outbound;
        var c = _parser.Parse("trojan://one@de.example.net:8443#C").Servers[0].Outbound;

        Assert.Equal(ServerImportKey.Compute(a), ServerImportKey.Compute(b));
        Assert.NotEqual(ServerImportKey.Compute(a), ServerImportKey.Compute(c));
    }
}
