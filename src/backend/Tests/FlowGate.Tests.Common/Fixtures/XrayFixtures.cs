namespace FlowGate.Tests.Common.Fixtures;

/// <summary>
///     Ответы CLI Xray, снятые с роутера (Xray 26.11.9)
/// </summary>
public static class XrayFixtures
{
    /// <summary>
    ///     Вывод <c>xray api bi -json bal-con-a bal-con-b</c>: объект на каждый тег по порядку аргументов, у первого
    ///     выбран резервный <c>out-con-a-1</c>, у второго — основной <c>out-con-b</c>
    /// </summary>
    public static string BalancerInfo => Load("balancer-info.txt");

    private static string Load(string fileName)
    {
        var resourceName = $"FlowGate.Tests.Common.Fixtures.Xray.{fileName}";
        using var stream = typeof(XrayFixtures).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Фикстура {resourceName} не найдена");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
