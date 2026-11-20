using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Состояние API Xray в памяти: после перезапуска FlowGate восстанавливается пересборкой конфига при старте
/// </summary>
public sealed class XrayApiState : IXrayApiState
{
    private volatile int _availability = (int)XrayApiAvailability.NotNeeded;

    /// <inheritdoc />
    public XrayApiAvailability Availability
    {
        get => (XrayApiAvailability)_availability;
        set => _availability = (int)value;
    }
}
