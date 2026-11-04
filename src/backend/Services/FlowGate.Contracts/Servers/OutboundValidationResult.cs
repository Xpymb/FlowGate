namespace FlowGate.Contracts.Servers;

/// <summary>
///     Результат проверки outbound
/// </summary>
/// <param name="Errors">Ошибки; пусто, если outbound корректен</param>
/// <param name="CheckedByXray">Проверен установленным Xray (<c>xray run -test</c>); false — только структурная проверка</param>
public sealed record OutboundValidationResult(IReadOnlyList<string> Errors, bool CheckedByXray)
{
    /// <summary>
    ///     Ошибок нет
    /// </summary>
    public bool IsValid => Errors.Count == 0;
}
