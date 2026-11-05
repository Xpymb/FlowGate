namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Параметры запуска долгоживущего процесса
/// </summary>
/// <param name="FileName">Исполняемый файл</param>
/// <param name="Arguments">Аргументы</param>
/// <param name="WorkingDirectory">Рабочий каталог или null</param>
public sealed record ProcessStartRequest(string FileName, IReadOnlyList<string> Arguments, string? WorkingDirectory = null);
