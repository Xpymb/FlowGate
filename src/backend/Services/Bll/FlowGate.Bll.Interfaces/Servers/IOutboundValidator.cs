using System.Text.Json.Nodes;

using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Interfaces.Servers;

/// <summary>
///     Проверка outbound: структурная и, если Xray установлен, через <c>xray run -test</c> установленной версии
/// </summary>
public interface IOutboundValidator
{
    /// <summary>
    ///     Проверяет outbound
    /// </summary>
    /// <param name="outbound">Outbound Xray</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ошибки и способ проверки</returns>
    Task<OutboundValidationResult> ValidateAsync(JsonObject outbound, CancellationToken cancellationToken);
}
