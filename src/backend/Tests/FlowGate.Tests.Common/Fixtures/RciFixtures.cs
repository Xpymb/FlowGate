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
