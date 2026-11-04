using System.Text;

using FlowGate.Bll.Implementations.Servers.Parsing;
using FlowGate.Contracts.Servers;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Тесты разбора ссылок в outbound Xray 26 (снимки JSON)
/// </summary>
public class ServerLinkParserTests
{
    /// <summary>
    ///     VLESS + XHTTP + REALITY даёт ровно тот outbound, что в редакторе на скриншоте
    /// </summary>
    [Fact]
    public void Parse_VlessXhttpReality_MatchesEditorSnapshot()
    {
        var server = ServerLinkParser.Parse(
            "vless://34f2bd76-fba8-4322-945c-500653e90c8f@132.243.213.13:443?type=xhttp&security=reality&sni=botapi.max.ru"
            + "&fp=firefox&pbk=kcY7j5H3u1eoN2QFKpSWZf4mGeJ0q-pkdG8yQe32an0&sid=e308d3326127&spx=%2F&path=%2F&mode=auto#cascade-out-2");

        Assert.Equal("cascade-out-2", server.Name);
        JsonAssert.Equal(
            """
            {
              "protocol": "vless",
              "settings": { "address": "132.243.213.13", "port": 443, "id": "34f2bd76-fba8-4322-945c-500653e90c8f", "encryption": "none" },
              "streamSettings": {
                "network": "xhttp",
                "security": "reality",
                "realitySettings": {
                  "serverName": "botapi.max.ru", "fingerprint": "firefox",
                  "publicKey": "kcY7j5H3u1eoN2QFKpSWZf4mGeJ0q-pkdG8yQe32an0", "shortId": "e308d3326127", "spiderX": "/"
                },
                "xhttpSettings": { "path": "/", "mode": "auto" }
              }
            }
            """,
            server.Outbound);
    }

    /// <summary>
    ///     VLESS TCP + Vision + TLS: flow, alpn, fingerprint; tcp становится raw
    /// </summary>
    [Fact]
    public void Parse_VlessTcpVisionTls_MapsFlowAndTls()
    {
        var server = ServerLinkParser.Parse(
            "vless://11111111-2222-3333-4444-555555555555@example.com:443?type=tcp&security=tls&flow=xtls-rprx-vision&sni=example.com&alpn=h2%2Chttp%2F1.1&fp=chrome");

        JsonAssert.Equal(
            """
            {
              "protocol": "vless",
              "settings": { "address": "example.com", "port": 443, "id": "11111111-2222-3333-4444-555555555555", "encryption": "none", "flow": "xtls-rprx-vision" },
              "streamSettings": {
                "network": "raw", "security": "tls",
                "tlsSettings": { "serverName": "example.com", "fingerprint": "chrome", "alpn": ["h2", "http/1.1"] }
              }
            }
            """,
            server.Outbound);
        Assert.Equal("example.com:443", server.Name);
    }

    /// <summary>
    ///     WebSocket с host и path, gRPC с multiMode, IPv6 в скобках
    /// </summary>
    [Fact]
    public void Parse_VlessWsAndGrpc_MapsTransports()
    {
        var ws = ServerLinkParser.Parse("vless://id@[2001:db8::1]:8443?type=ws&security=tls&host=cdn.example.com&path=%2Fws%3Fed%3D2048#WS");
        var grpc = ServerLinkParser.Parse("vless://id@example.com:443?type=grpc&security=tls&serviceName=svc&mode=multi#gRPC");

        Assert.Equal("2001:db8::1", ws.Outbound["settings"]!["address"]!.GetValue<string>());
        JsonAssert.Equal("""{ "path": "/ws?ed=2048", "host": "cdn.example.com" }""", ws.Outbound["streamSettings"]!["wsSettings"]);
        JsonAssert.Equal("""{ "serviceName": "svc", "multiMode": true }""", grpc.Outbound["streamSettings"]!["grpcSettings"]);
    }

    /// <summary>
    ///     Trojan без security получает TLS по умолчанию
    /// </summary>
    [Fact]
    public void Parse_TrojanWithoutSecurity_DefaultsToTls()
    {
        var server = ServerLinkParser.Parse("trojan://p%40ss@example.com:443?sni=example.com#%F0%9F%87%A9%F0%9F%87%AA%20DE");

        Assert.Equal("🇩🇪 DE", server.Name);
        JsonAssert.Equal(
            """
            {
              "protocol": "trojan",
              "settings": { "address": "example.com", "port": 443, "password": "p@ss" },
              "streamSettings": { "network": "raw", "security": "tls", "tlsSettings": { "serverName": "example.com" } }
            }
            """,
            server.Outbound);
    }

    /// <summary>
    ///     VMess в формате v2rayN (base64 JSON) с WS и TLS
    /// </summary>
    [Fact]
    public void Parse_VmessV2rayN_MapsFields()
    {
        var json = """{"v":"2","ps":"VMess WS","add":"example.com","port":"443","id":"uuid-1","aid":"0","scy":"auto","net":"ws","type":"none","host":"cdn.example.com","path":"/vm","tls":"tls","sni":"example.com"}""";
        var link = "vmess://" + Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var server = ServerLinkParser.Parse(link);

        Assert.Equal("VMess WS", server.Name);
        JsonAssert.Equal(
            """
            {
              "protocol": "vmess",
              "settings": { "address": "example.com", "port": 443, "id": "uuid-1", "security": "auto" },
              "streamSettings": {
                "network": "ws", "security": "tls",
                "wsSettings": { "path": "/vm", "host": "cdn.example.com" },
                "tlsSettings": { "serverName": "example.com" }
              }
            }
            """,
            server.Outbound);
    }

    /// <summary>
    ///     Shadowsocks: plaintext SIP002, base64 userinfo и устаревшая base64-форма дают одинаковый outbound
    /// </summary>
    [Fact]
    public void Parse_ShadowsocksForms_GiveSameOutbound()
    {
        var userInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes("chacha20-ietf-poly1305:secret"));
        var legacy = Convert.ToBase64String(Encoding.UTF8.GetBytes("chacha20-ietf-poly1305:secret@1.2.3.4:8388"));

        var plain = ServerLinkParser.Parse("ss://chacha20-ietf-poly1305:secret@1.2.3.4:8388#SS");
        var sip002 = ServerLinkParser.Parse($"ss://{userInfo.TrimEnd('=')}@1.2.3.4:8388/?plugin=#SS");
        var old = ServerLinkParser.Parse($"ss://{legacy}#SS");

        const string expected = """
            { "protocol": "shadowsocks", "settings": { "address": "1.2.3.4", "port": 8388, "method": "chacha20-ietf-poly1305", "password": "secret" } }
            """;
        JsonAssert.Equal(expected, plain.Outbound);
        JsonAssert.Equal(expected, sip002.Outbound);
        JsonAssert.Equal(expected, old.Outbound);
    }

    /// <summary>
    ///     Ключ Shadowsocks 2022 проверяется по длине
    /// </summary>
    [Fact]
    public void Parse_Shadowsocks2022_ValidatesKeyLength()
    {
        var goodKey = Convert.ToBase64String(new byte[16]);
        var badKey = Convert.ToBase64String(new byte[10]);

        var server = ServerLinkParser.Parse($"ss://2022-blake3-aes-128-gcm:{Uri.EscapeDataString(goodKey)}@1.2.3.4:443#2022");

        Assert.Equal(goodKey, server.Outbound["settings"]!["password"]!.GetValue<string>());
        Assert.Throws<ServerLinkFormatException>(() => ServerLinkParser.Parse($"ss://2022-blake3-aes-128-gcm:{Uri.EscapeDataString(badKey)}@1.2.3.4:443"));
    }

    /// <summary>
    ///     Hysteria2: hy2 со слешем и без дают одинаковый outbound, salamander уходит в finalmask
    /// </summary>
    [Fact]
    public void Parse_Hysteria2_WithAndWithoutSlash_SameOutbound()
    {
        const string query = "?sni=example.com&insecure=1&obfs=salamander&obfs-password=mask#HY2";
        var withSlash = ServerLinkParser.Parse("hysteria2://p%2Fw@example.com:443/" + query);
        var withoutSlash = ServerLinkParser.Parse("hy2://p%2Fw@example.com:443" + query);

        const string expected = """
            {
              "protocol": "hysteria",
              "settings": { "version": 2, "address": "example.com", "port": 443 },
              "streamSettings": {
                "network": "hysteria", "security": "tls",
                "tlsSettings": { "serverName": "example.com", "allowInsecure": true },
                "hysteriaSettings": { "version": 2, "auth": "p/w" },
                "finalmask": { "udp": [ { "type": "salamander", "settings": { "password": "mask" } } ] }
              }
            }
            """;
        JsonAssert.Equal(expected, withSlash.Outbound);
        JsonAssert.Equal(expected, withoutSlash.Outbound);
    }

    /// <summary>
    ///     Некорректные ссылки отклоняются с понятной причиной
    /// </summary>
    [Theory]
    [InlineData("tuic://id@example.com:443", "не поддерживается")]
    [InlineData("vless://id@example.com:443?security=reality&sni=a.com", "pbk")]
    [InlineData("vless://id@example.com:443?type=kcp", "kcp")]
    [InlineData("vless://id@example.com:99999", "порт")]
    [InlineData("vless://id@example.com", "порта")]
    [InlineData("ss://aes-256-gcm:pass@1.2.3.4:8388?plugin=obfs-local%3Bobfs%3Dhttp", "SIP003")]
    [InlineData("ss://rc4-md5:pass@1.2.3.4:8388", "rc4-md5")]
    [InlineData("hy2://auth@example.com:443,5000-6000", "порт")]
    [InlineData("hy2://auth@example.com:443/path", "«/»")]
    [InlineData("just text", "не ссылка")]
    public void Parse_InvalidLink_ThrowsWithReason(string link, string reason)
    {
        var exception = Assert.Throws<ServerLinkFormatException>(() => ServerLinkParser.Parse(link));

        Assert.Contains(reason, exception.Message);
    }

    /// <summary>
    ///     Пароль не попадает в сообщение об ошибке
    /// </summary>
    [Fact]
    public void Parse_InvalidLink_DoesNotLeakSecret()
    {
        var exception = Assert.Throws<ServerLinkFormatException>(
            () => ServerLinkParser.Parse("trojan://TopSecretPassword@example.com:443?type=quic"));

        Assert.DoesNotContain("TopSecretPassword", exception.Message);
    }
}
