namespace FlowGate.Contracts.Xray;

/// <summary>
///     Балансировщик Xray (стратегия <c>leastLoad</c>): выбирает живой outbound по замерам observatory с учётом весов
/// </summary>
/// <param name="Tag">Уникальный тег</param>
/// <param name="Selector">Теги outbound-кандидатов</param>
/// <param name="FallbackTag">Outbound, если живых кандидатов нет или замеров ещё нет</param>
/// <param name="Costs">Вес кандидата по тегу: чем больше, тем реже выбирается</param>
/// <param name="Tolerance">Допустимая доля неудачных замеров; 0 — без ограничения</param>
public sealed record XrayBalancer(
    string Tag,
    IReadOnlyList<string> Selector,
    string FallbackTag,
    IReadOnlyDictionary<string, double> Costs,
    double Tolerance);
