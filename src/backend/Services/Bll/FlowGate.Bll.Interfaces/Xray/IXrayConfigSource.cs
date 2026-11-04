using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Источник модели конфигурации Xray (соединения, серверы, правила)
/// </summary>
public interface IXrayConfigSource
{
    /// <summary>
    ///     Собирает модель из текущего состояния
    /// </summary>
    /// <param name="logLevel">Уровень логирования из настроек</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Модель конфигурации</returns>
    Task<XrayConfigModel> GetModelAsync(string logLevel, CancellationToken cancellationToken);
}
