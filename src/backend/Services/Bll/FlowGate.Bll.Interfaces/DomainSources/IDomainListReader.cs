using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;

namespace FlowGate.Bll.Interfaces.DomainSources;

/// <summary>
///     Чтение каталога и списков любого источника с кешем: собранный список переиспользуется, пока не изменился файл
///     источника
/// </summary>
public interface IDomainListReader
{
    /// <summary>
    ///     Возвращает кеш каталога источника
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Каталог или null, если источник ещё не загружен</returns>
    Task<SourceListCatalog?> GetCatalogAsync(DomainSource source, CancellationToken cancellationToken);

    /// <summary>
    ///     Собирает список
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="name">Имя списка</param>
    /// <param name="attributeFilter">Фильтр или null</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Содержимое для Keenetic</returns>
    /// <exception cref="DomainListException">Если источник не загружен, списка нет или он не собирается</exception>
    Task<ListContent> ReadAsync(DomainSource source, string name, string? attributeFilter, CancellationToken cancellationToken);

    /// <summary>
    ///     Строит каталог заново по файлу и сохраняет его в кеш
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="filePath">Файл (кандидат или действующий) или null для локального</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Каталог</returns>
    /// <exception cref="DomainListException">Если файл повреждён</exception>
    Task<SourceListCatalog> BuildCatalogAsync(DomainSource source, string? filePath, CancellationToken cancellationToken);

    /// <summary>
    ///     Забывает собранные списки источника
    /// </summary>
    /// <param name="sourceId">Источник</param>
    void Invalidate(string sourceId);
}
