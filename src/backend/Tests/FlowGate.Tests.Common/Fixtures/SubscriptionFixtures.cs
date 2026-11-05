using System.Text;

namespace FlowGate.Tests.Common.Fixtures;

/// <summary>
///     Синтетические подписки (без настоящих ключей) в форматах реальных провайдеров
/// </summary>
public static class SubscriptionFixtures
{
    /// <summary>
    ///     Xray-JSON в духе Axo: профиль с одним сервером, профиль с тремя и балансировщиком, профиль с цепочкой и неизвестным протоколом
    /// </summary>
    public static string XrayJsonProfiles => Load("xray-json-profiles.json");

    /// <summary>
    ///     Обычный список ссылок с метаданными в виде строк <c>#key: value</c>
    /// </summary>
    /// <param name="trojanPassword">Пароль trojan-сервера (для проверки ротации учётных данных)</param>
    /// <returns>Текст подписки</returns>
    public static string PlainList(string trojanPassword = "secret1") =>
        $"""
        #profile-title: base64:{Convert.ToBase64String(Encoding.UTF8.GetBytes("Мой VPN"))}
        #profile-update-interval: 6
        #subscription-userinfo: upload=100; download=200; total=1000; expire=1893456000
        vless://00000000-0000-0000-0000-000000000001@nl.example.net:443?type=tcp&security=reality&sni=www.example.com&pbk=PUBLICKEY1&sid=ab#NL
        trojan://{trojanPassword}@de.example.net:443?sni=de.example.net#DE
        hy2://auth@fi.example.net:443/?sni=fi.example.net#FI
        """;

    /// <summary>
    ///     Тот же список в base64, как отдаёт большинство провайдеров
    /// </summary>
    /// <param name="trojanPassword">Пароль trojan-сервера</param>
    /// <returns>Текст подписки</returns>
    public static string Base64List(string trojanPassword = "secret1") =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(PlainList(trojanPassword)));

    private static string Load(string fileName)
    {
        var resourceName = $"FlowGate.Tests.Common.Fixtures.Subscriptions.{fileName}";
        using var stream = typeof(SubscriptionFixtures).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Фикстура {resourceName} не найдена");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
