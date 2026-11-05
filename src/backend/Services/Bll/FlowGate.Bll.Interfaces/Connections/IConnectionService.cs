using FlowGate.Contracts.Connections;
using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Connections;

namespace FlowGate.Bll.Interfaces.Connections;

/// <summary>
///     Соединения для API: inbound Xray + интерфейс Keenetic + выбранный сервер
/// </summary>
public interface IConnectionService
{
    /// <summary>
    ///     Возвращает все соединения с состоянием интерфейсов. Если Keenetic не ответил, состояние пустое
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Соединения</returns>
    Task<IReadOnlyList<ConnectionDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает соединение с состоянием интерфейса
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Соединение</returns>
    /// <exception cref="EntityNotFoundException">Если соединения нет</exception>
    Task<ConnectionDto> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Создаёт соединение: выбирает порт и интерфейс, применяет конфигурацию Xray, создаёт и включает интерфейс
    ///     Keenetic. При ошибке всё созданное откатывается
    /// </summary>
    /// <param name="request">Имя, вид и сервер</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданное соединение</returns>
    /// <exception cref="RequestValidationException">Если имя, вид или сервер некорректны</exception>
    /// <exception cref="ConflictException">Если кончились свободные порты или адреса WireGuard</exception>
    /// <exception cref="XrayOperationException">Если Xray отклонил конфигурацию</exception>
    /// <exception cref="KeeneticCommandException">Если Keenetic отклонил команды</exception>
    Task<ConnectionDto> CreateAsync(ConnectionCreateRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Меняет имя и сервер соединения. Новый сервер применяется пересборкой конфигурации Xray
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="request">Имя и сервер</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённое соединение</returns>
    /// <exception cref="EntityNotFoundException">Если соединения нет</exception>
    /// <exception cref="RequestValidationException">Если имя или сервер некорректны</exception>
    /// <exception cref="XrayOperationException">Если Xray отклонил конфигурацию; соединение при этом не меняется</exception>
    Task<ConnectionDto> UpdateAsync(string id, ConnectionUpdateRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет соединение вместе с его интерфейсом Keenetic
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если соединения нет</exception>
    /// <exception cref="ConflictException">Если на соединение назначены DNS-маршруты</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Сверяет интерфейсы соединений с Keenetic: пропавшие создаёт заново, разошедшиеся настройки чинит,
    ///     административное состояние (включён/выключен пользователем) не трогает. Чужие интерфейсы не меняет
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько интерфейсов исправлено и какие созданы заново</returns>
    Task<ConnectionReconcileResult> ReconcileAsync(CancellationToken cancellationToken);
}
