using FlowGate.Exceptions;
using FlowGate.SharedContracts.Dto.DomainSources;
using FlowGate.SharedContracts.Dto.Jobs;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.DomainSources;

/// <summary>
///     Источники списков для API: CRUD, обновление, каталог, предпросмотр, локальные списки
/// </summary>
public interface IDomainSourceService
{
    /// <summary>
    ///     Возвращает все источники
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Источники</returns>
    Task<IReadOnlyList<DomainSourceDto>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает источник
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Источник</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    Task<DomainSourceDto> GetAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Добавляет источник и сразу запускает его загрузку (кроме локального)
    /// </summary>
    /// <param name="request">Параметры</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Созданный источник</returns>
    /// <exception cref="RequestValidationException">Если параметры некорректны</exception>
    Task<DomainSourceDto> CreateAsync(DomainSourceSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Изменяет источник; при смене адреса файл загружается заново
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="request">Параметры (вид не меняется)</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Изменённый источник</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="RequestValidationException">Если параметры некорректны или у встроенного меняется адрес</exception>
    Task<DomainSourceDto> UpdateAsync(string id, DomainSourceSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет источник вместе с файлами
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="RequestValidationException">Если источник встроенный</exception>
    /// <exception cref="ConflictException">Если его списки используются в маршрутах</exception>
    Task DeleteAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Запускает обновление источника; после изменения применяются маршруты, которые его используют
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="via">Как загружать: по настройкам, только через прокси обновлений или напрямую</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Фоновая задача</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="ConflictException">Если обновление этого источника уже идёт</exception>
    Task<JobDto> RefreshAsync(string id, UpdateVia via, CancellationToken cancellationToken);

    /// <summary>
    ///     Обновляет по очереди источники, которым пора: с автообновлением, используемые маршрутами, старше интервала
    /// </summary>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сколько источников обновлялось</returns>
    Task<int> RefreshDueAsync(CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает каталог списков источника
    /// </summary>
    /// <param name="id">Идентификатор</param>
    /// <param name="query">Подстрока имени или null</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Списки; пусто, если источник не загружен</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    Task<IReadOnlyList<DomainListSummaryDto>> GetListsAsync(string id, string? query, CancellationToken cancellationToken);

    /// <summary>
    ///     Собирает список с фильтром для предпросмотра
    /// </summary>
    /// <param name="id">Источник</param>
    /// <param name="name">Список</param>
    /// <param name="attributeFilter">Фильтр или null</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Предпросмотр</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="RequestValidationException">Если список не собирается или фильтр некорректен</exception>
    Task<DomainListPreviewDto> PreviewAsync(string id, string name, string? attributeFilter, CancellationToken cancellationToken);

    /// <summary>
    ///     Возвращает тексты списков локального источника
    /// </summary>
    /// <param name="id">Локальный источник</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Списки</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="RequestValidationException">Если источник не локальный</exception>
    Task<IReadOnlyList<LocalListDto>> GetLocalListsAsync(string id, CancellationToken cancellationToken);

    /// <summary>
    ///     Создаёт или заменяет список локального источника; текст проверяется целиком
    /// </summary>
    /// <param name="id">Локальный источник</param>
    /// <param name="name">Имя списка</param>
    /// <param name="request">Текст</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Сохранённый список</returns>
    /// <exception cref="EntityNotFoundException">Если источника нет</exception>
    /// <exception cref="RequestValidationException">Если имя или текст некорректны (ошибки по строкам в Details)</exception>
    Task<LocalListDto> SaveLocalListAsync(string id, string name, LocalListSaveRequest request, CancellationToken cancellationToken);

    /// <summary>
    ///     Удаляет список локального источника
    /// </summary>
    /// <param name="id">Локальный источник</param>
    /// <param name="name">Имя списка</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <exception cref="EntityNotFoundException">Если источника или списка нет</exception>
    /// <exception cref="ConflictException">Если список используется в маршруте</exception>
    Task DeleteLocalListAsync(string id, string name, CancellationToken cancellationToken);
}
