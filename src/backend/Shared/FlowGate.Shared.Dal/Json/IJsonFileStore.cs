namespace FlowGate.Shared.Dal.Json;

/// <summary>
///     Хранилище одного объекта в JSON-файле. Объект кэшируется в памяти, поэтому тип должен быть неизменяемым
/// </summary>
/// <typeparam name="T">Тип хранимого объекта</typeparam>
public interface IJsonFileStore<T>
    where T : class
{
    /// <summary>
    ///     Полный путь к файлу
    /// </summary>
    string FilePath { get; }

    /// <summary>
    ///     Читает объект; если файла нет, возвращает значение по умолчанию (файл при этом не создаётся)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Хранимый объект</returns>
    /// <exception cref="InvalidDataException">Если файл повреждён</exception>
    Task<T> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Атомарно записывает объект: во временный файл, затем переименованием поверх старого
    /// </summary>
    /// <param name="value">Новое значение</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task WriteAsync(T value, CancellationToken cancellationToken);

    /// <summary>
    ///     Читает, изменяет и записывает объект под одной блокировкой
    /// </summary>
    /// <param name="update">Функция, возвращающая новое значение по текущему</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Записанное значение</returns>
    /// <exception cref="InvalidDataException">Если файл повреждён</exception>
    Task<T> UpdateAsync(Func<T, T> update, CancellationToken cancellationToken);
}
