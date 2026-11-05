namespace FlowGate.Shared.Keenetic.Models;

/// <summary>
///     Коды ошибок NDMS, проверенные на KeeneticOS 5.2 (см. docs/keenetic-findings.md)
/// </summary>
public static class RciErrorCodes
{
    /// <summary>
    ///     <c>no such command</c>
    /// </summary>
    public const string NoSuchCommand = "7405600";

    /// <summary>
    ///     <c>argument parse error</c>: неверный аргумент, в том числе удаление несуществующего объекта
    /// </summary>
    public const string ArgumentParseError = "7405602";

    /// <summary>
    ///     <c>too many elements</c>: превышен лимит доменов в <c>object-group fqdn</c>
    /// </summary>
    public const string TooManyElements = "101583600";
}
