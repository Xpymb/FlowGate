using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.DnsRoutes;
using FlowGate.SharedContracts.Dto.Jobs;

namespace FlowGate.Bll.Interfaces.DnsRoutes;

/// <summary>
///     DNS-маршруты: список источника → соединение. Изменения сохраняются сразу, а в Keenetic применяются фоновой
///     задачей <c>dns-routes-apply</c>
/// </summary>
public interface IDnsRouteService
{
    /// <summary>
    ///     Возвращает все маршруты
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Маршруты</returns>
    Task<IReadOnlyList<DnsRouteDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает маршрут
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Маршрут</returns>
    /// <exception cref="EntityNotFoundException">Если маршрута нет</exception>
    Task<DnsRouteDto> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Создаёт маршрут и запускает применение: в Keenetic он появится включённым
    /// </summary>
    /// <param name="request">Источник, список, соединение, фильтр</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданный маршрут</returns>
    /// <exception cref="RequestValidationException">Если источника, списка или соединения нет, фильтр некорректен, список слишком большой</exception>
    /// <exception cref="ConflictException">Если такой маршрут уже есть</exception>
    Task<DnsRouteDto> CreateAsync(DnsRouteCreateRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Меняет соединение и фильтр; правило переносится на новый интерфейс с прежним флагом
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="request">Соединение и фильтр</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённый маршрут</returns>
    /// <exception cref="EntityNotFoundException">Если маршрута нет</exception>
    /// <exception cref="RequestValidationException">Если соединения нет или фильтр некорректен</exception>
    Task<DnsRouteDto> UpdateAsync(string id, DnsRouteUpdateRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет маршрут; его группы и правила убираются из Keenetic при применении
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если маршрута нет</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Включает или выключает правило в Keenetic по явной просьбе пользователя
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="disabled">true — выключить, false — включить</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Маршрут</returns>
    /// <exception cref="EntityNotFoundException">Если маршрута нет</exception>
    Task<DnsRouteDto> SetDisabledAsync(string id, bool disabled, CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает применение всех маршрутов. Если применение уже идёт, оно повторится по окончании
    /// </summary>
    /// <returns>Фоновая задача (текущая, если уже идёт)</returns>
    JobDto RequestApply();

    /// <summary>
    ///     Применяет маршруты сразу (для сверки). Если идёт фоновое применение — ничего не делает
    /// </summary>
    /// <param name="recreatedInterfaces">Интерфейсы, которые сверка только что создала заново</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько команд отправлено в Keenetic; -1, если пропущено</returns>
    Task<int> ReconcileAsync(IReadOnlySet<string> recreatedInterfaces, CancellationToken cancellationToken);
}
