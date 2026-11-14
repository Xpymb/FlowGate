using FlowGate.Contracts.Connections;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Contracts.DomainSources;
using FlowGate.SharedContracts.Dto.DnsRoutes;

namespace FlowGate.Bll.Implementations.DnsRoutes;

/// <summary>
///     Маппинг маршрутов в DTO
/// </summary>
public static class DnsRouteMapper
{
    /// <summary>
    ///     Преобразует маршрут в DTO
    /// </summary>
    /// <param name="route">Маршрут</param>
    /// <param name="source">Источник или null, если удалён</param>
    /// <param name="connection">Соединение или null, если удалено</param>
    /// <returns>DTO</returns>
    /// <exception cref="ArgumentNullException">Если route равен null</exception>
    public static DnsRouteDto ToDto(this DnsRoute route, DomainSource? source, Connection? connection)
    {
        ArgumentNullException.ThrowIfNull(route);

        var state = !route.Provisioned ? "Pending" : route.LastKnownDisabled ? "Disabled" : "Enabled";
        return new DnsRouteDto(
            route.Id,
            route.SourceId,
            source?.Name,
            route.ListName,
            route.AttributeFilter,
            route.ConnectionId,
            connection?.Name,
            connection?.InterfaceName,
            route.GroupBaseName,
            state,
            route.RequestedExclusive ?? route.Exclusive,
            route.LastApply is { } apply
                ? new DnsRouteApplyDto(
                    apply.At,
                    apply.Status.ToString(),
                    apply.Error,
                    apply.Entries,
                    apply.Skipped,
                    apply.Parts,
                    apply.Warnings,
                    apply.ContentChangedAt)
                : null,
            route.CreatedAt,
            route.UpdatedAt);
    }
}
