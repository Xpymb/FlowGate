using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using FlowGate.Shared.Dal.Files;

namespace FlowGate.Shared.Dal.Json;

/// <summary>
///     Хранилище объекта в JSON-файле с атомарной записью и правами 600 на Linux
/// </summary>
/// <typeparam name="T">Тип хранимого объекта</typeparam>
public sealed class JsonFileStore<T> : IJsonFileStore<T>, IDisposable
    where T : class
{
    private readonly JsonTypeInfo<T> _typeInfo;
    private readonly Func<T> _createDefault;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T? _cached;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="JsonFileStore{T}"/>
    /// </summary>
    /// <param name="filePath">Полный путь к файлу</param>
    /// <param name="typeInfo">Source-generated метаданные сериализации</param>
    /// <param name="createDefault">Значение, если файла ещё нет</param>
    /// <exception cref="ArgumentNullException">Если typeInfo или createDefault равны null</exception>
    public JsonFileStore(string filePath, JsonTypeInfo<T> typeInfo, Func<T> createDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        FilePath = Path.GetFullPath(filePath);
        _typeInfo = typeInfo ?? throw new ArgumentNullException(nameof(typeInfo));
        _createDefault = createDefault ?? throw new ArgumentNullException(nameof(createDefault));
    }

    /// <inheritdoc />
    public string FilePath { get; }

    /// <inheritdoc />
    public async Task<T> ReadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _cached ??= await LoadAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task WriteAsync(T value, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(value);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await SaveAsync(value, cancellationToken);
            _cached = value;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<T> UpdateAsync(Func<T, T> update, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var current = _cached ??= await LoadAsync(cancellationToken);
            var updated = update(current) ?? throw new InvalidOperationException("Функция обновления вернула null");
            await SaveAsync(updated, cancellationToken);
            _cached = updated;
            return updated;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();

    private async Task<T> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath))
        {
            return _createDefault();
        }

        try
        {
            await using var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync(stream, _typeInfo, cancellationToken) ?? _createDefault();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Файл {FilePath} повреждён: {ex.Message}", ex);
        }
    }

    private Task SaveAsync(T value, CancellationToken cancellationToken) =>
        AtomicFile.WriteAsync(
            FilePath,
            (stream, token) => JsonSerializer.SerializeAsync(stream, value, _typeInfo, token),
            cancellationToken);
}
