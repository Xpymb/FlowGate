using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Маппинг серверов в DTO
/// </summary>
public static class ServerMapper
{
    /// <summary>
    ///     Преобразует сервер в строку списка
    /// </summary>
    /// <param name="server">Сервер</param>
    /// <returns>DTO без outbound</returns>
    /// <exception cref="ArgumentNullException">Если server равен null</exception>
    public static ServerDto ToDto(this Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        var summary = server.Summary;
        return new ServerDto(
            server.Id,
            server.Name,
            summary.Protocol,
            summary.Address,
            summary.Port,
            summary.Network,
            summary.Security,
            summary.ServerName,
            server.SourceKind.ToString(),
            server.SubscriptionId,
            server.UpdatedAt,
            server.ImportKey);
    }

    /// <summary>
    ///     Преобразует сервер в DTO с outbound
    /// </summary>
    /// <param name="server">Сервер</param>
    /// <returns>DTO для JSON-редактора</returns>
    /// <exception cref="ArgumentNullException">Если server равен null</exception>
    public static ServerDetailsDto ToDetailsDto(this Server server)
    {
        ArgumentNullException.ThrowIfNull(server);

        return new ServerDetailsDto(
            server.ToDto(),
            (JsonObject)server.Outbound.DeepClone(),
            server.SourceKind == ServerSourceKind.Manual);
    }

    /// <summary>
    ///     Преобразует разобранный текст в предпросмотр
    /// </summary>
    /// <param name="batch">Результат разбора</param>
    /// <param name="title">Название профиля от провайдера или null</param>
    /// <returns>Серверы с ключами идентичности, отклонённые узлы и предупреждения</returns>
    /// <exception cref="ArgumentNullException">Если batch равен null</exception>
    public static ServerPreviewDto ToPreviewDto(this ServerImportBatch batch, string? title)
    {
        ArgumentNullException.ThrowIfNull(batch);

        return new ServerPreviewDto(
            title,
            batch.Servers.Select(ToPreviewItem).ToList(),
            batch.Rejected.Select(rejection => new ServerImportRejectionDto(rejection.Index, rejection.Reason)).ToList(),
            batch.Warnings);
    }

    private static ServerPreviewItemDto ToPreviewItem(ParsedServer server)
    {
        var summary = OutboundSummary.From(server.Outbound);
        return new ServerPreviewItemDto(
            ServerImportKey.Compute(server.Outbound),
            server.Name,
            summary.Protocol,
            summary.Address,
            summary.Port,
            summary.Network,
            summary.Security);
    }
}
