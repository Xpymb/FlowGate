namespace FlowGate.Contracts.Xray;

/// <summary>
///     Наблюдение за outbound (<c>burstObservatory</c>): периодические HTTP-запросы через outbound для балансировщиков
/// </summary>
/// <param name="SubjectSelector">Теги наблюдаемых outbound</param>
/// <param name="ProbeUrl">Адрес проверки, например <c>https://www.gstatic.com/generate_204</c></param>
/// <param name="Interval">Интервал проверок в формате Xray, например <c>1m</c></param>
/// <param name="Sampling">Сколько последних замеров учитывать</param>
/// <param name="Timeout">Таймаут одной проверки в формате Xray, например <c>5s</c></param>
public sealed record XrayObservatory(
    IReadOnlyList<string> SubjectSelector,
    string ProbeUrl,
    string Interval,
    int Sampling,
    string Timeout);
