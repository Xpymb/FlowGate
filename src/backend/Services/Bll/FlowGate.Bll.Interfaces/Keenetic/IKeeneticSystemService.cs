using FlowGate.SharedContracts.Dto.Keenetic;

namespace FlowGate.Bll.Interfaces.Keenetic;

/// <summary>
///     Сведения о роутере Keenetic
/// </summary>
public interface IKeeneticSystemService
{
    /// <summary>
    ///     Читает модель, версию прошивки и компоненты роутера и проверяет совместимость с FlowGate
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сведения о роутере</returns>
    /// <exception cref="FlowGate.Exceptions.KeeneticUnavailableException">Если RCI недоступен или ответ не разобран</exception>
    Task<KeeneticSystemInfoDto> GetSystemInfoAsync(CancellationToken cancellationToken);
}
