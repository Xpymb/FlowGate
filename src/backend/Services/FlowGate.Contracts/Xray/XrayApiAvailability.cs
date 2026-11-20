namespace FlowGate.Contracts.Xray;

/// <summary>
///     Есть ли в действующем конфиге Xray блок API
/// </summary>
public enum XrayApiAvailability
{
    /// <summary>
    ///     API не нужен: балансировщиков нет
    /// </summary>
    NotNeeded,

    /// <summary>
    ///     API включён
    /// </summary>
    Enabled,

    /// <summary>
    ///     Установленный Xray отклонил конфиг с API, действует конфиг без него
    /// </summary>
    Rejected,
}
