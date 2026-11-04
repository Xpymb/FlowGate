using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Установленная версия Xray (по выводу <c>xray version</c>, с кэшем до подмены бинарника)
/// </summary>
public interface IXrayVersionProvider
{
    /// <summary>
    ///     Бинарник Xray существует
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    ///     Возвращает установленную версию
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Версия или null, если Xray не установлен или версию не удалось определить</returns>
    Task<XrayVersion?> GetInstalledVersionAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Сбрасывает кэш после подмены бинарника
    /// </summary>
    void Invalidate();
}
