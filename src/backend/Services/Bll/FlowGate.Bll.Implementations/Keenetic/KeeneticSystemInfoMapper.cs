using FlowGate.Contracts.Keenetic;
using FlowGate.SharedContracts.Dto.Keenetic;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Маппинг <see cref="KeeneticSystemInfo"/> в <see cref="KeeneticSystemInfoDto"/>
/// </summary>
public static class KeeneticSystemInfoMapper
{
    /// <summary>
    ///     Преобразует сущность в DTO
    /// </summary>
    /// <param name="info">Сведения о роутере</param>
    /// <returns>DTO для API</returns>
    /// <exception cref="ArgumentNullException">Если info равен null</exception>
    public static KeeneticSystemInfoDto ToDto(this KeeneticSystemInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);

        return new KeeneticSystemInfoDto(
            info.Model,
            info.HardwareId,
            info.Release,
            info.Title,
            info.Architecture,
            info.IsSupported,
            info.MissingComponents,
            info.Problems);
    }
}
