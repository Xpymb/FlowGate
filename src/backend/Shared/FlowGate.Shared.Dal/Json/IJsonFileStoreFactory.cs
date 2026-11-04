using System.Text.Json.Serialization.Metadata;

namespace FlowGate.Shared.Dal.Json;

/// <summary>
///     Создаёт хранилища JSON-файлов в каталоге данных приложения
/// </summary>
public interface IJsonFileStoreFactory
{
    /// <summary>
    ///     Создаёт хранилище файла в каталоге <c>FlowGate:DataDirectory</c>
    /// </summary>
    /// <typeparam name="T">Тип хранимого объекта</typeparam>
    /// <param name="fileName">Имя файла, например <c>servers.json</c></param>
    /// <param name="typeInfo">Source-generated метаданные сериализации</param>
    /// <param name="createDefault">Значение, если файла ещё нет</param>
    /// <returns>Хранилище; создавай одно на файл и держи его синглтоном</returns>
    /// <exception cref="ArgumentException">Если fileName содержит путь</exception>
    IJsonFileStore<T> Create<T>(string fileName, JsonTypeInfo<T> typeInfo, Func<T> createDefault)
        where T : class;
}
