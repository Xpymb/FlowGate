using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Options;

using FlowGate.Shared.Options;

namespace FlowGate.Shared.Dal.Json;

/// <summary>
///     Фабрика хранилищ JSON-файлов в <see cref="FlowGateOptions.DataDirectory"/>
/// </summary>
public sealed class JsonFileStoreFactory : IJsonFileStoreFactory
{
    private readonly string _dataDirectory;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="JsonFileStoreFactory"/>
    /// </summary>
    /// <param name="options">Общие настройки приложения</param>
    /// <exception cref="ArgumentNullException">Если options равен null</exception>
    public JsonFileStoreFactory(IOptions<FlowGateOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataDirectory = Path.GetFullPath(options.Value.DataDirectory);
    }

    /// <inheritdoc />
    public IJsonFileStore<T> Create<T>(string fileName, JsonTypeInfo<T> typeInfo, Func<T> createDefault)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Ожидается имя файла без пути: {fileName}", nameof(fileName));
        }

        return new JsonFileStore<T>(Path.Combine(_dataDirectory, fileName), typeInfo, createDefault);
    }
}
