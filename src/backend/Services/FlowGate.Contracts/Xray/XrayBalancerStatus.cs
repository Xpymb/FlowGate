namespace FlowGate.Contracts.Xray;

/// <summary>
///     Выбор балансировщиков Xray
/// </summary>
/// <param name="Support">Удалось ли узнать выбор</param>
/// <param name="ActiveTags">Тег выбранного outbound по тегу балансировщика; null — замеров ещё нет, Xray берёт
///     <c>fallbackTag</c></param>
public sealed record XrayBalancerStatus(XrayBalancerSupport Support, IReadOnlyDictionary<string, string?> ActiveTags)
{
    /// <summary>
    ///     Статус без выбора
    /// </summary>
    /// <param name="support">Причина</param>
    /// <returns>Статус с пустым словарём</returns>
    public static XrayBalancerStatus Without(XrayBalancerSupport support) => new(support, new Dictionary<string, string?>());
}
