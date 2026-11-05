using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Servers;

/// <summary>
///     Ключ идентичности сервера: SHA-256 от протокола, адреса, порта и параметров транспорта, без учётных данных.
///     Смена пароля или UUID провайдером оставляет ключ прежним, поэтому ID сервера в подписке не меняется
/// </summary>
public static class ServerImportKey
{
    /// <summary>
    ///     Вычисляет ключ
    /// </summary>
    /// <param name="outbound">Outbound Xray</param>
    /// <returns>SHA-256 в нижнем регистре hex</returns>
    /// <exception cref="ArgumentNullException">Если outbound равен null</exception>
    public static string Compute(JsonObject outbound)
    {
        var summary = OutboundSummary.From(outbound);
        var identity = string.Join(
            '\n',
            summary.Protocol,
            summary.Address.ToLowerInvariant(),
            summary.Port?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            summary.Network,
            summary.Security,
            summary.ServerName.ToLowerInvariant(),
            summary.Path,
            summary.Mode,
            summary.ServiceName);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }
}
