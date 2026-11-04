using System.Text.Json;

using FlowGate.Exceptions;
using FlowGate.Shared.Keenetic.Models;

namespace FlowGate.Shared.Keenetic;

/// <summary>
///     Клиент RCI Keenetic (<c>http://127.0.0.1:79/rci/</c>)
/// </summary>
public interface IKeeneticRciClient
{
    /// <summary>
    ///     Читает объект RCI, например <c>show/version</c>
    /// </summary>
    /// <param name="path">Путь после <c>/rci/</c></param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>JSON-ответ</returns>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен или ответил ошибкой HTTP</exception>
    Task<JsonElement> GetAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    ///     Выполняет CLI-команды одним запросом <c>POST /rci/</c> с <c>[{"parse": ...}]</c>.
    ///     Ошибки команд не бросаются: их нужно проверить в результате
    /// </summary>
    /// <param name="commands">Команды в порядке выполнения</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат по каждой команде</returns>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен или ответ не удалось разобрать</exception>
    Task<RciBatchResult> ExecuteAsync(IReadOnlyList<string> commands, CancellationToken cancellationToken);

    /// <summary>
    ///     Сохраняет running-config в постоянную конфигурацию (<c>system configuration save</c>)
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="KeeneticCommandException">Если Keenetic отклонил сохранение</exception>
    /// <exception cref="KeeneticUnavailableException">Если RCI недоступен</exception>
    Task SaveConfigurationAsync(CancellationToken cancellationToken);
}
