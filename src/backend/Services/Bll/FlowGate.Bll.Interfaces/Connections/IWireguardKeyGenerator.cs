using FlowGate.Contracts.Connections;
using FlowGate.Exceptions;

namespace FlowGate.Bll.Interfaces.Connections;

/// <summary>
///     Создание ключей WireGuard
/// </summary>
public interface IWireguardKeyGenerator
{
    /// <summary>
    ///     Создаёт новую пару ключей
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Пара ключей</returns>
    /// <exception cref="RequestValidationException">Если генератор недоступен (Xray не установлен)</exception>
    /// <exception cref="XrayOperationException">Если генератор вернул неожиданный ответ</exception>
    Task<WireguardKeyPair> GenerateAsync(CancellationToken cancellationToken);
}
