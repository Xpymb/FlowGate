using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.SharedContracts.Dto.DomainSources;

namespace FlowGate.Bll.Implementations.DomainSources;

/// <summary>
///     Маппинг источников и списков в DTO
/// </summary>
public static class DomainSourceMapper
{
    /// <summary>
    ///     Преобразует источник в DTO
    /// </summary>
    /// <param name="source">Источник</param>
    /// <param name="downloaded">Есть ли загруженный файл</param>
    /// <param name="listCount">Списков в каталоге</param>
    /// <param name="routeCount">Маршрутов, использующих источник</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если source равен null</exception>
    public static DomainSourceDto ToDto(this DomainSource source, bool downloaded, int listCount, int routeCount)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new DomainSourceDto(
            source.Id,
            source.Name,
            source.Kind.ToString(),
            source.Repository,
            source.Branch,
            source.Dat?.ReleaseRepository,
            source.Dat?.Asset,
            source.Dat?.ChecksumAsset,
            source.Dat?.Url,
            source.Dat?.ChecksumUrl,
            source.AutoUpdate,
            source.BuiltIn,
            downloaded,
            source.Downloaded?.Revision,
            listCount,
            routeCount,
            source.LastRefresh is { } refresh
                ? new DomainSourceRefreshDto(refresh.At, refresh.Status.ToString(), refresh.Error, refresh.Lists, refresh.Errors)
                : null);
    }

    /// <summary>
    ///     Преобразует запись каталога в DTO
    /// </summary>
    /// <param name="entry">Запись каталога</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если entry равен null</exception>
    public static DomainListSummaryDto ToDto(this CatalogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new DomainListSummaryDto(entry.Name, entry.Domains, entry.Ipv4, entry.Ipv6, entry.Skipped, entry.Attributes);
    }

    /// <summary>
    ///     Преобразует локальный список в DTO
    /// </summary>
    /// <param name="list">Список</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если list равен null</exception>
    public static LocalListDto ToDto(this LocalList list)
    {
        ArgumentNullException.ThrowIfNull(list);
        return new LocalListDto(list.Name, list.Text, list.UpdatedAt);
    }
}
