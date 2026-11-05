using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;

namespace FlowGate.Bll.Interfaces.DomainSources;

/// <summary>
///     Чтение списков источника одного вида. Все виды приводятся к одному результату: каталог и содержимое списка
/// </summary>
public interface IListSourceReader
{
    /// <summary>
    ///     Вид источника, который читает реализация
    /// </summary>
    DomainSourceKind Kind { get; }

    /// <summary>
    ///     Строит каталог списков
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="filePath">Загруженный файл или null для локального источника</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Каталог с ошибками отдельных списков</returns>
    /// <exception cref="DomainListException">Если файл повреждён целиком</exception>
    Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Собирает один список
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="filePath">Загруженный файл или null для локального источника</param>
    /// <param name="name">Имя списка</param>
    /// <param name="attributeFilter">Фильтр <c>attr</c> / <c>!attr</c> или null</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Содержимое для Keenetic</returns>
    /// <exception cref="DomainListException">Если списка нет, он не собирается или фильтр неприменим</exception>
    Task<ListContent> ReadListAsync(DomainSource source, string? filePath, string name, string? attributeFilter, CancellationToken cancellationToken);
}
