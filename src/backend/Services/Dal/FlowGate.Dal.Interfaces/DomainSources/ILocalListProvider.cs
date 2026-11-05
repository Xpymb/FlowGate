using FlowGate.Contracts.DomainSources;

namespace FlowGate.Dal.Interfaces.DomainSources;

/// <summary>
///     Хранение списков локальных источников (<c>local-lists.json</c>)
/// </summary>
public interface ILocalListProvider
{
    /// <summary>
    ///     Возвращает списки источника
    /// </summary>
    /// <param name="sourceId">Локальный источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Списки по имени</returns>
    Task<IReadOnlyList<LocalList>> GetAllAsync(string sourceId, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет или заменяет список
    /// </summary>
    /// <param name="list">Список</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task UpsertAsync(LocalList list, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет список
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <param name="name">Имя списка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если список был</returns>
    Task<bool> DeleteAsync(string sourceId, string name, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет все списки источника
    /// </summary>
    /// <param name="sourceId">Источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    Task DeleteSourceAsync(string sourceId, CancellationToken cancellationToken);
}
