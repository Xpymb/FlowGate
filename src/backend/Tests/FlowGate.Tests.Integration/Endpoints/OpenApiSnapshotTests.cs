namespace FlowGate.Tests.Integration.Endpoints;

/// <summary>
///     Снимок OpenAPI-документа, из которого фронт генерирует типы API (<c>src/frontend/api/openapi.json</c>).
///     Обновление снимка: запустить тест с переменной окружения <c>FLOWGATE_UPDATE_OPENAPI=1</c>
/// </summary>
public sealed class OpenApiSnapshotTests : IDisposable
{
    private const string UpdateVariable = "FLOWGATE_UPDATE_OPENAPI";

    private readonly FlowGateWebApplicationFactory _factory = new();

    /// <summary>
    ///     Снимок схемы во фронте совпадает с документом, который отдаёт бэкенд
    /// </summary>
    [Fact]
    public async Task GetOpenApi_SnapshotInFrontend_MatchesDocument()
    {
        using var client = _factory.CreateClient();
        var snapshotPath = GetSnapshotPath();

        var document = Normalize(await client.GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
            await File.WriteAllTextAsync(snapshotPath, document, TestContext.Current.CancellationToken);
            return;
        }

        Assert.True(File.Exists(snapshotPath), $"Нет снимка {snapshotPath}: запусти тест с {UpdateVariable}=1");
        var snapshot = Normalize(await File.ReadAllTextAsync(snapshotPath, TestContext.Current.CancellationToken));
        Assert.True(
            snapshot == document,
            $"OpenAPI бэкенда изменился: обнови снимок ({UpdateVariable}=1) и типы фронта (npm run gen:api)");
    }

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static string Normalize(string text) => text.ReplaceLineEndings("\n").TrimEnd() + "\n";

    private static string GetSnapshotPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FlowGate.slnx")))
        {
            directory = directory.Parent;
        }

        if (directory is null)
        {
            throw new InvalidOperationException("Не найден каталог с FlowGate.slnx");
        }

        return Path.GetFullPath(Path.Combine(directory.FullName, "..", "frontend", "api", "openapi.json"));
    }
}
