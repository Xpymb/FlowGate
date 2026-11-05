namespace FlowGate.SharedContracts.Dto.Xray;

/// <summary>
///     Последние строки вывода Xray
/// </summary>
/// <param name="Lines">Строки, старые первыми</param>
public sealed record XrayLogDto(IReadOnlyList<XrayLogLineDto> Lines);
