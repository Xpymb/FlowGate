using System.Text.Json.Serialization;

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
