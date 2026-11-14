using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Options;

using FlowGate.Shared.Changes;
using FlowGate.Shared.Options;

namespace FlowGate.Shared.Dal.Json;

/// <summary>
///     Фабрика хранилищ JSON-файлов в <see cref="FlowGateOptions.DataDirectory"/>
/// </summary>
public sealed class JsonFileStoreFactory : IJsonFileStoreFactory
{
    private readonly string _dataDirectory;
    private readonly IChangeFeed? _changes;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="JsonFileStoreFactory"/>
    /// </summary>
    /// <param name="options">Общие настройки приложения</param>
    /// <param name="changes">Шина изменений, в которую хранилища сообщают о записи; null — не сообщать</param>
    /// <exception cref="ArgumentNullException">Если options равен null</exception>
    public JsonFileStoreFactory(IOptions<FlowGateOptions> options, IChangeFeed? changes = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _dataDirectory = Path.GetFullPath(options.Value.DataDirectory);
        _changes = changes;
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

        return new JsonFileStore<T>(Path.Combine(_dataDirectory, fileName), typeInfo, createDefault, _changes);
    }
}
