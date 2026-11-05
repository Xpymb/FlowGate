namespace FlowGate.Contracts.Maintenance;

/// <summary>
///     Итог очистки Keenetic от объектов FlowGate
/// </summary>
/// <param name="Commands">Команды NDMS по порядку (при пробном запуске — только показаны)</param>
/// <param name="Routes">Снято правил <c>dns-proxy route</c></param>
/// <param name="Groups">Удалено групп <c>object-group fqdn</c></param>
/// <param name="Interfaces">Удалено интерфейсов</param>
/// <param name="Errors">Ошибки шагов; очистка продолжается после ошибки шага</param>
public sealed record CleanupReport(
    IReadOnlyList<string> Commands,
    int Routes,
    int Groups,
    int Interfaces,
    IReadOnlyList<string> Errors)
{
    /// <summary>
    ///     Все шаги выполнены без ошибок
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
