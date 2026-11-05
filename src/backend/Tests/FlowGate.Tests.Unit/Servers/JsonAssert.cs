using System.Text.Json.Nodes;

namespace FlowGate.Tests.Unit.Servers;

/// <summary>
///     Сравнение JSON без учёта порядка свойств
/// </summary>
public static class JsonAssert
{
    /// <summary>
    ///     Проверяет, что JSON совпадает с ожидаемым
    /// </summary>
    /// <param name="expected">Ожидаемый JSON</param>
    /// <param name="actual">Фактический узел</param>
    public static void Equal(string expected, JsonNode? actual)
    {
        var expectedNode = JsonNode.Parse(expected);
        Assert.True(
            JsonNode.DeepEquals(expectedNode, actual),
            $"Ожидалось:{Environment.NewLine}{expectedNode}{Environment.NewLine}Получено:{Environment.NewLine}{actual}");
    }
}
