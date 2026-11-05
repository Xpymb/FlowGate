using System.Runtime.Versioning;

using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.Options;

namespace FlowGate.Tests.Unit.Dal;

/// <summary>
///     Тесты <see cref="JsonFileStore{T}"/> и <see cref="JsonFileStoreFactory"/> на временном каталоге
/// </summary>
public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Нет файла — возвращается значение по умолчанию, файл не создаётся
    /// </summary>
    [Fact]
    public async Task ReadAsync_FileMissing_ReturnsDefaultWithoutCreatingFile()
    {
        using var sut = CreateStore("missing.json");

        var result = await sut.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(TestDocument.Default, result);
        Assert.False(File.Exists(sut.FilePath));
    }

    /// <summary>
    ///     Записанное значение читает новое хранилище того же файла
    /// </summary>
    [Fact]
    public async Task WriteAsync_ThenReadFromNewStore_ReturnsSameValue()
    {
        var document = new TestDocument(7, "Ёлка");
        using (var writer = CreateStore("data.json"))
        {
            await writer.WriteAsync(document, TestContext.Current.CancellationToken);
        }

        using var reader = CreateStore("data.json");
        var result = await reader.ReadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(document, result);
    }

    /// <summary>
    ///     После записи не остаётся временных файлов, отсутствующий каталог создаётся
    /// </summary>
    [Fact]
    public async Task WriteAsync_DirectoryMissing_CreatesItAndLeavesNoTemporaryFiles()
    {
        using var sut = CreateStore("nested.json", Path.Combine(_directory, "a", "b"));

        await sut.WriteAsync(new TestDocument(1, "x"), TestContext.Current.CancellationToken);
        await sut.WriteAsync(new TestDocument(2, "y"), TestContext.Current.CancellationToken);

        var files = Directory.GetFiles(Path.GetDirectoryName(sut.FilePath)!);
        Assert.Equal([sut.FilePath], files);
    }

    /// <summary>
    ///     Параллельные обновления не теряются
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ConcurrentUpdates_AppliesAll()
    {
        using var sut = CreateStore("counter.json");

        await Task.WhenAll(Enumerable.Range(0, 50).Select(_ => sut.UpdateAsync(
            document => document with { Counter = document.Counter + 1 },
            TestContext.Current.CancellationToken)));

        using var reader = CreateStore("counter.json");
        var result = await reader.ReadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(50, result.Counter);
    }

    /// <summary>
    ///     Повреждённый файл не подменяется значением по умолчанию
    /// </summary>
    [Fact]
    public async Task ReadAsync_CorruptedFile_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(_directory);
        await File.WriteAllTextAsync(Path.Combine(_directory, "broken.json"), "{ not json", TestContext.Current.CancellationToken);
        using var sut = CreateStore("broken.json");

        await Assert.ThrowsAsync<InvalidDataException>(() => sut.ReadAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    ///     На Linux файл получает права 600
    /// </summary>
    [Fact]
    [UnsupportedOSPlatform("windows")]
    public async Task WriteAsync_OnUnix_SetsOwnerOnlyPermissions()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Права UNIX проверяются только на Linux");
        using var sut = CreateStore("secret.json");

        await sut.WriteAsync(new TestDocument(1, "secret"), TestContext.Current.CancellationToken);

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(sut.FilePath));
    }

    /// <summary>
    ///     Фабрика кладёт файл в каталог данных
    /// </summary>
    [Fact]
    public void Create_FileName_PlacesFileInDataDirectory()
    {
        var sut = CreateFactory();

        var store = sut.Create("servers.json", TestDocumentJsonContext.Default.TestDocument, () => TestDocument.Default);

        Assert.Equal(Path.Combine(Path.GetFullPath(_directory), "servers.json"), store.FilePath);
    }

    /// <summary>
    ///     Фабрика не принимает путь вместо имени файла
    /// </summary>
    [Theory]
    [InlineData("../escape.json")]
    [InlineData("sub/servers.json")]
    public void Create_FileNameWithPath_ThrowsArgumentException(string fileName)
    {
        var sut = CreateFactory();

        Assert.Throws<ArgumentException>(
            () => sut.Create(fileName, TestDocumentJsonContext.Default.TestDocument, () => TestDocument.Default));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private JsonFileStore<TestDocument> CreateStore(string fileName, string? directory = null) =>
        new(
            Path.Combine(directory ?? _directory, fileName),
            TestDocumentJsonContext.Default.TestDocument,
            () => TestDocument.Default);

    private JsonFileStoreFactory CreateFactory() =>
        new(Microsoft.Extensions.Options.Options.Create(new FlowGateOptions { DataDirectory = _directory }));
}
