namespace FlowGate.Tests.Common.Keenetic;

/// <summary>
///     Группа <c>object-group fqdn</c> в подменном Keenetic
/// </summary>
public sealed class FakeDnsGroup
{
    /// <summary>
    ///     Описание
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///     Элементы в порядке добавления
    /// </summary>
    public List<string> Include { get; } = [];
}
