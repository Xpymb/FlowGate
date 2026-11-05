using System.Text.Json.Serialization;

namespace FlowGate.Shared.Keenetic.Serialization;

/// <summary>
///     Элемент тела <c>POST /rci/</c>: одна CLI-команда
/// </summary>
/// <param name="Parse">Текст команды</param>
public sealed record RciParseRequest([property: JsonPropertyName("parse")] string Parse);
