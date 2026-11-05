using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace FlowGate.Contracts.Servers;

/// <summary>
///     Сервер — исходящее соединение Xray. Хранится готовым outbound без поля <c>tag</c>: тег назначается при сборке конфига
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя для пользователя</param>
/// <param name="Outbound">Outbound Xray; объект изменяемый, поэтому наружу из хранилища отдаётся копия</param>
/// <param name="SourceKind">Откуда взялся сервер</param>
/// <param name="SubscriptionId">Подписка-источник или null для ручного сервера</param>
/// <param name="ImportKey">Ключ идентичности без учётных данных (<see cref="ServerImportKey"/>)</param>
/// <param name="CreatedAt">Когда добавлен</param>
/// <param name="UpdatedAt">Когда последний раз изменён</param>
public sealed record Server(
    string Id,
    string Name,
    JsonObject Outbound,
    ServerSourceKind SourceKind,
    string? SubscriptionId,
    string ImportKey,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    ///     Сводка для списка серверов
    /// </summary>
    [JsonIgnore]
    public OutboundSummary Summary => OutboundSummary.From(Outbound);

    /// <summary>
    ///     Возвращает копию с независимым outbound
    /// </summary>
    /// <returns>Глубокая копия</returns>
    public Server DeepCopy() => this with { Outbound = (JsonObject)Outbound.DeepClone() };
}
