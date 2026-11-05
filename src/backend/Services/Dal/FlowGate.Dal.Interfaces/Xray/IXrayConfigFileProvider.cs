namespace FlowGate.Dal.Interfaces.Xray;

/// <summary>
///     Хранение файла конфигурации Xray. Запись в два шага: кандидат рядом с файлом, затем атомарная замена,
///     чтобы между ними проверить кандидат через <c>xray run -test</c>
/// </summary>
public interface IXrayConfigFileProvider
{
    /// <summary>
    ///     Полный путь к действующему файлу конфигурации
    /// </summary>
    string ConfigPath { get; }

    /// <summary>
    ///     Читает действующий файл
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Содержимое или null, если файла нет</returns>
    Task<byte[]?> ReadAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Записывает кандидат рядом с действующим файлом (права 600 на Linux)
    /// </summary>
    /// <param name="content">Содержимое</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Путь к кандидату</returns>
    Task<string> WriteCandidateAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken);

    /// <summary>
    ///     Атомарно заменяет действующий файл кандидатом
    /// </summary>
    /// <param name="candidatePath">Путь, полученный из <see cref="WriteCandidateAsync"/></param>
    void Commit(string candidatePath);

    /// <summary>
    ///     Удаляет отклонённый кандидат
    /// </summary>
    /// <param name="candidatePath">Путь, полученный из <see cref="WriteCandidateAsync"/></param>
    void Discard(string candidatePath);
}
