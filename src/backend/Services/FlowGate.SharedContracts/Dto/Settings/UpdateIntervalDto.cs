namespace FlowGate.SharedContracts.Dto.Settings;

/// <summary>
///     Интервал в часах
/// </summary>
/// <param name="Hours">Заданный пользователем или null</param>
/// <param name="DefaultHours">Значение из конфигурации</param>
/// <param name="EffectiveHours">Действующее значение</param>
/// <param name="MaxHours">Наибольшее допустимое значение</param>
public sealed record UpdateIntervalDto(int? Hours, int DefaultHours, int EffectiveHours, int MaxHours);
