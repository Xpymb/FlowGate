using System.Text.Json.Nodes;

namespace FlowGate.Contracts.Xray;

/// <summary>
///     Исходящее соединение Xray (outbound). Описание хранится целиком, потому что подписки отдают его сырым Xray-JSON
/// </summary>
/// <param name="Tag">Уникальный тег; при сборке конфига перезаписывает <c>tag</c> в описании</param>
/// <param name="Definition">Объект outbound: <c>protocol</c>, <c>settings</c>, <c>streamSettings</c> и т.д.</param>
public sealed record XrayOutbound(string Tag, JsonObject Definition);
