using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Есть ли API в действующем конфиге Xray; ставит <see cref="IXrayConfigService"/> при каждой записи конфига
/// </summary>
public interface IXrayApiState
{
    /// <summary>
    ///     Состояние API по последней записи конфига
    /// </summary>
    XrayApiAvailability Availability { get; set; }
}
