using System.Text.Json.Nodes;

using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Сборка <c>xray.json</c> из модели
/// </summary>
public interface IXrayConfigBuilder
{
    /// <summary>
    ///     Собирает конфигурацию: log, inbounds, outbounds (плюс встроенные direct и block), routing
    /// </summary>
    /// <param name="model">Модель конфигурации</param>
    /// <returns>JSON-конфигурация Xray</returns>
    /// <exception cref="XrayOperationException">Если модель противоречива: повторяющиеся теги, правило на несуществующий тег, inbound без правила</exception>
    JsonObject Build(XrayConfigModel model);
}
