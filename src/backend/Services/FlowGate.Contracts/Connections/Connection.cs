using System.Text.Json.Serialization;

using FlowGate.Contracts.Servers;

namespace FlowGate.Contracts.Connections;

/// <summary>
///     Соединение: inbound Xray на локальном порту, интерфейс Keenetic, который в него подключается, и сервер,
///     через который уходит трафик
/// </summary>
/// <param name="Id">Идентификатор</param>
/// <param name="Name">Имя для пользователя</param>
/// <param name="Kind">Вид соединения; после создания не меняется</param>
/// <param name="ServerId">Сервер (outbound)</param>
/// <param name="Port">Порт inbound Xray</param>
/// <param name="InterfaceName">Интерфейс Keenetic, например <c>Proxy0</c> или <c>Wireguard1</c></param>
/// <param name="Wireguard">Ключи и адрес для WireGuard, для остальных видов null</param>
/// <param name="CreatedAt">Когда создано</param>
/// <param name="UpdatedAt">Когда последний раз изменено</param>
public sealed record Connection(
    string Id,
    string Name,
    ConnectionKind Kind,
    string ServerId,
    int Port,
    string InterfaceName,
    WireguardSettings? Wireguard,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    /// <summary>
    ///     Начало описания интерфейса Keenetic: по нему FlowGate узнаёт свои интерфейсы
    /// </summary>
    public const string DescriptionPrefix = "FlowGate: ";

    /// <summary>
    ///     Наибольшее число резервных серверов у соединения
    /// </summary>
    public const int MaxFallbacks = 3;

    /// <summary>
    ///     Резервные серверы по убыванию приоритета: Xray уводит на них трафик, если основной не отвечает
    /// </summary>
    public IReadOnlyList<string> FallbackServerIds { get; init; } = [];

    /// <summary>
    ///     Основной сервер и резервные по порядку; индекс — слот кандидата
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<string> CandidateServerIds => [ServerId, .. FallbackServerIds];

    /// <summary>
    ///     Тег балансировщика Xray, если у соединения есть резервные серверы
    /// </summary>
    [JsonIgnore]
    public string BalancerTag => "bal-" + Id;

    /// <summary>
    ///     Тег inbound в конфигурации Xray
    /// </summary>
    [JsonIgnore]
    public string InboundTag => "in-" + Id;

    /// <summary>
    ///     Тег outbound в конфигурации Xray
    /// </summary>
    [JsonIgnore]
    public string OutboundTag => "out-" + Id;

    /// <summary>
    ///     Возвращает тег outbound кандидата: у основного (слот 0) — <see cref="OutboundTag"/>, у резервных —
    ///     <c>out-&lt;id&gt;-&lt;слот&gt;</c>
    /// </summary>
    /// <param name="slot">Слот кандидата в <see cref="CandidateServerIds"/></param>
    /// <returns>Тег outbound</returns>
    public string CandidateOutboundTag(int slot) => slot == 0 ? OutboundTag : $"{OutboundTag}-{slot}";

    /// <summary>
    ///     Возвращает кандидатов, чьи серверы существуют, по убыванию приоритета: при двух и больше трафик
    ///     распределяет балансировщик Xray, при одном — прямое правило, без кандидатов — <c>block</c>
    /// </summary>
    /// <param name="servers">Серверы по идентификатору</param>
    /// <returns>Кандидаты со слотами и тегами outbound</returns>
    public IReadOnlyList<ConnectionCandidate> AliveCandidates(IReadOnlyDictionary<string, Server> servers)
    {
        ArgumentNullException.ThrowIfNull(servers);

        return CandidateServerIds
            .Select((serverId, slot) => (slot, server: servers.GetValueOrDefault(serverId)))
            .Where(item => item.server is not null)
            .Select(item => new ConnectionCandidate(item.slot, CandidateOutboundTag(item.slot), item.server!))
            .ToList();
    }

    /// <summary>
    ///     Описание интерфейса Keenetic
    /// </summary>
    [JsonIgnore]
    public string InterfaceDescription => DescriptionPrefix + Name;

    /// <summary>
    ///     Переносит ли соединение UDP (QUIC, звонки)
    /// </summary>
    [JsonIgnore]
    public bool SupportsUdp => Kind == ConnectionKind.Wireguard;

    /// <summary>
    ///     Возвращает префикс имени интерфейса Keenetic для вида соединения
    /// </summary>
    /// <param name="kind">Вид соединения</param>
    /// <returns><c>Proxy</c> или <c>Wireguard</c></returns>
    public static string InterfacePrefix(ConnectionKind kind) =>
        kind == ConnectionKind.Wireguard ? "Wireguard" : "Proxy";
}
