using FlowGate.Contracts.Xray;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Установка и обновление Xray: скачать, проверить, подменить бинарник, при неудаче откатиться
/// </summary>
public interface IXrayInstaller
{
    /// <summary>
    ///     Находит релиз и запускает фоновую установку
    /// </summary>
    /// <param name="version">Версия; null — последняя стабильная</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Запущенная задача</returns>
    /// <exception cref="EntityNotFoundException">Если релиза нет</exception>
    /// <exception cref="ConflictException">Если установка уже идёт</exception>
    /// <exception cref="ExternalServiceUnavailableException">Если GitHub недоступен</exception>
    Task<JobSnapshot> StartInstallAsync(XrayVersion? version, CancellationToken cancellationToken);
}
