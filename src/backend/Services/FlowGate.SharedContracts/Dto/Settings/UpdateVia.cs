using System.Text.Json.Serialization;

namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Как загружать обновление
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<UpdateVia>))]
public enum UpdateVia
{
    /// <summary>
    ///     По настройкам: через прокси обновлений, если он включён и доступен, иначе напрямую
    /// </summary>
    Auto,

    /// <summary>
    ///     Только через прокси обновлений (даже если он выключен в настройках); недоступен — ошибка
    /// </summary>
    Proxy,

    /// <summary>
    ///     Напрямую, без прокси
    /// </summary>
    Direct,
}
