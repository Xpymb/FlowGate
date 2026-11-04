using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Кольцевой буфер последних строк вывода Xray
/// </summary>
public interface IXrayLogBuffer
{
    /// <summary>
    ///     Добавляет строку; самая старая вытесняется при переполнении
    /// </summary>
    /// <param name="line">Строка вывода</param>
    void Add(string line);

    /// <summary>
    ///     Возвращает последние строки
    /// </summary>
    /// <param name="count">Сколько строк вернуть</param>
    /// <returns>Строки, старые первыми</returns>
    IReadOnlyList<XrayLogLine> Tail(int count);
}
