using FlowGate.Contracts.Servers;

namespace FlowGate.Bll.Interfaces.Servers;

/// <summary>
///     Разбор текста с серверами: ссылки (по одной на строку), base64 от них или Xray-JSON
/// </summary>
public interface IServerImportParser
{
    /// <summary>
    ///     Разбирает текст; ошибки отдельных узлов не прерывают разбор остальных
    /// </summary>
    /// <param name="text">Текст: вставленный пользователем или тело подписки</param>
    /// <returns>Серверы, отклонённые узлы, предупреждения и метаданные из тела</returns>
    ServerImportBatch Parse(string text);
}
