namespace FlowGate.Tests.Common.Fixtures;

/// <summary>
///     Ответы GitHub, снятые с реального API (релизы XTLS/Xray-core, ассеты сокращены до нужных)
/// </summary>
public static class GitHubFixtures
{
    /// <summary>
    ///     <c>GET /repos/XTLS/Xray-core/releases?per_page=3</c>: v26.10.31 и v26.10.21 стабильные, v26.9.30 — pre-release
    /// </summary>
    public static string XrayReleases => Load("xray-releases.json");

    /// <summary>
    ///     Настоящий <c>Xray-linux-arm64-v8a.zip.dgst</c> релиза v26.10.31
    /// </summary>
    public static string XrayDigest => Load("Xray-linux-arm64-v8a.zip.dgst.txt");

    private static string Load(string fileName)
    {
        var resourceName = $"FlowGate.Tests.Common.Fixtures.GitHub.{fileName}";
        using var stream = typeof(GitHubFixtures).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Фикстура {resourceName} не найдена");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
