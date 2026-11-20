using System.Text.Json.Nodes;

using FlowGate.Contracts.Xray;

namespace FlowGate.Contracts.UpdateProxy;

/// <summary>
///     Модель прокси обновлений: HTTP-inbound на <c>127.0.0.1</c> и outbound выбранного сервера, весь трафик inbound
///     уходит через сервер. Через inbound ходят только клиенты обновлений FlowGate и opkg, поэтому список доменов не нужен
/// </summary>
public static class UpdateProxyXrayModel
{
    /// <summary>
    ///     Тег inbound прокси обновлений
    /// </summary>
    public const string InboundTag = "in-update-proxy";

    /// <summary>
    ///     Тег outbound выбранного сервера
    /// </summary>
    public const string OutboundTag = "out-update-proxy";

    /// <summary>
    ///     Адрес прослушивания: прокси нужен только самому FlowGate
    /// </summary>
    public const string ListenAddress = "127.0.0.1";

    /// <summary>
    ///     Строит отдельную модель с одним прокси (экземпляр скрипта самообновления)
    /// </summary>
    /// <param name="logLevel">Уровень логирования Xray</param>
    /// <param name="outbound">Outbound сервера; копируется</param>
    /// <param name="port">Порт HTTP-inbound</param>
    /// <returns>Модель для <c>IXrayConfigBuilder</c></returns>
    public static XrayConfigModel Build(string logLevel, JsonObject outbound, int port) =>
        Append(XrayConfigModel.Empty(logLevel), outbound, port);

    /// <summary>
    ///     Добавляет прокси обновлений к модели основного экземпляра
    /// </summary>
    /// <param name="model">Модель соединений</param>
    /// <param name="outbound">Outbound сервера; копируется</param>
    /// <param name="port">Порт HTTP-inbound</param>
    /// <returns>Новая модель с inbound, outbound и правилом прокси в конце списков</returns>
    public static XrayConfigModel Append(XrayConfigModel model, JsonObject outbound, int port)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(outbound);

        return model with
        {
            Inbounds = [.. model.Inbounds, new XrayInbound(InboundTag, "http", ListenAddress, port, null, Sniffing: false)],
            Outbounds = [.. model.Outbounds, new XrayOutbound(OutboundTag, (JsonObject)outbound.DeepClone())],
            Rules = [.. model.Rules, new XrayRoutingRule([InboundTag], OutboundTag)],
        };
    }
}
