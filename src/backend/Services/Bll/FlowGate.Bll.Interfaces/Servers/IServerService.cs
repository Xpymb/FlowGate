using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.Servers;

namespace FlowGate.Bll.Interfaces.Servers;

/// <summary>
///     Серверы для API: список, разбор ссылки, проверка и правка outbound, импорт
/// </summary>
public interface IServerService
{
    /// <summary>
    ///     Возвращает все серверы
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Серверы без outbound</returns>
    Task<IReadOnlyList<ServerDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает сервер с outbound
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сервер</returns>
    /// <exception cref="EntityNotFoundException">Если сервера нет</exception>
    Task<ServerDetailsDto> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Разбирает ссылку или Xray-JSON в черновик для редактора, ничего не сохраняя
    /// </summary>
    /// <param name="request">Ссылка или JSON</param>
    /// <returns>Имя и outbound первого найденного сервера</returns>
    /// <exception cref="RequestValidationException">Если сервер разобрать не удалось</exception>
    ServerDraftDto Parse(ServerParseRequest request);

    /// <summary>
    ///     Проверяет outbound установленным Xray
    /// </summary>
    /// <param name="request">Outbound</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Ошибки проверки</returns>
    Task<ServerValidationDto> ValidateAsync(ServerValidateRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Создаёт ручной сервер после проверки outbound
    /// </summary>
    /// <param name="request">Имя и outbound</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданный сервер</returns>
    /// <exception cref="RequestValidationException">Если имя пустое или outbound не прошёл проверку (ошибки в Details)</exception>
    Task<ServerDetailsDto> CreateAsync(ServerSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Изменяет ручной сервер после проверки outbound
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="request">Имя и outbound</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённый сервер</returns>
    /// <exception cref="EntityNotFoundException">Если сервера нет</exception>
    /// <exception cref="RequestValidationException">Если сервер из подписки или outbound не прошёл проверку</exception>
    Task<ServerDetailsDto> UpdateAsync(string id, ServerSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет ручной сервер
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если сервера нет</exception>
    /// <exception cref="RequestValidationException">Если сервер из подписки</exception>
    /// <exception cref="ConflictException">Если сервер выбран в соединении</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Импортирует несколько серверов как ручные; уже существующие (по ключу идентичности) пропускаются
    /// </summary>
    /// <param name="request">Текст со ссылками или Xray-JSON</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько добавлено, пропущено и отклонено</returns>
    Task<ServerImportResultDto> ImportAsync(ServerImportRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Разбирает текст со ссылками или Xray-JSON в список серверов, ничего не сохраняя
    /// </summary>
    /// <param name="request">Текст со ссылками или Xray-JSON</param>
    /// <returns>Серверы с ключами идентичности, отклонённые узлы и предупреждения</returns>
    /// <exception cref="RequestValidationException">Если текст пустой</exception>
    ServerPreviewDto Preview(ServerImportRequest request);
}
