using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Генерация и применение <c>xray.json</c>
/// </summary>
public interface IXrayConfigService
{
    /// <summary>
    ///     Пересобирает конфигурацию и записывает её, если она изменилась. Если указан бинарник, конфигурация
    ///     сначала проверяется им (<c>xray run -test</c>): новая — до записи, неизменная — как есть
    /// </summary>
    /// <param name="testBinaryPath">Бинарник для проверки или null, чтобы не проверять</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если файл изменился</returns>
    /// <exception cref="XrayOperationException">Если Xray отклонил конфигурацию; действующий файл при этом не меняется</exception>
    Task<bool> WriteAsync(string? testBinaryPath, CancellationToken cancellationToken);

    /// <summary>
    ///     Пересобирает и проверяет конфигурацию установленным Xray, при изменении перезапускает работающий процесс
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>true, если файл изменился</returns>
    /// <exception cref="XrayOperationException">Если Xray отклонил конфигурацию или не перезапустился</exception>
    Task<bool> ApplyAsync(CancellationToken cancellationToken);
}
