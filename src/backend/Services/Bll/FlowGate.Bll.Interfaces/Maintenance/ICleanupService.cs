using FlowGate.Contracts.Maintenance;

namespace FlowGate.Bll.Interfaces.Maintenance;

/// <summary>
///     Очистка Keenetic перед удалением пакета (<c>flowgate cleanup</c>)
/// </summary>
public interface ICleanupService
{
    /// <summary>
    ///     Останавливает xray и удаляет из конфигурации Keenetic всё, что создал FlowGate: DNS-маршруты, группы
    ///     <c>fg-*</c> и интерфейсы с описанием <c>FlowGate: …</c>. Чужие объекты не трогает. Данные FlowGate остаются;
    ///     маршруты помечаются несозданными с прежним флагом выключенности, чтобы после повторной установки сверка
    ///     вернула их как были (opkg вызывает удаление и при <c>--force-reinstall</c>)
    /// </summary>
    /// <param name="dryRun">Только собрать команды, ничего не менять</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Итог очистки</returns>
    Task<CleanupReport> RunAsync(bool dryRun, CancellationToken cancellationToken);
}
