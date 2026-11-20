using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Bll.Interfaces.Servers;

/// <summary>
///     Пинг серверов: временный экземпляр Xray с socks-inbound на каждый сервер и HTTP-запрос через каждый из них
/// </summary>
public interface IServerPingService
{
    /// <summary>
    ///     Пингует серверы; одновременно выполняется один пинг, следующий ждёт
    /// </summary>
    /// <param name="request">Серверы</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Результат на каждый запрошенный сервер в том же порядке</returns>
    /// <exception cref="RequestValidationException">Если список пуст или длиннее допустимого</exception>
    /// <exception cref="ConflictException">Если Xray не установлен</exception>
    Task<IReadOnlyList<ServerPingResultDto>> PingAsync(ServerPingRequest request, CancellationToken cancellationToken);
}
