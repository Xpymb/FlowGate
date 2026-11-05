namespace FlowGate.SharedContracts.Dto.Keenetic;

/// <summary>
///     Сведения о роутере и его совместимости с FlowGate
/// </summary>
/// <param name="Model">Модель, например <c>Hopper SE (KN-3812)</c></param>
/// <param name="HardwareId">Индекс модели, например <c>KN-3812</c></param>
/// <param name="Release">Версия прошивки, например <c>5.02.B.2.0-2</c></param>
/// <param name="Title">Название версии, например <c>5.2 Beta 2</c></param>
/// <param name="Architecture">Архитектура, например <c>aarch64</c></param>
/// <param name="IsSupported">Роутер подходит для FlowGate</param>
/// <param name="MissingComponents">Компоненты KeeneticOS, которые нужно установить</param>
/// <param name="Problems">Причины несовместимости для показа пользователю</param>
public sealed record KeeneticSystemInfoDto(
    string Model,
    string HardwareId,
    string Release,
    string Title,
    string Architecture,
    bool IsSupported,
    IReadOnlyList<string> MissingComponents,
    IReadOnlyList<string> Problems);
