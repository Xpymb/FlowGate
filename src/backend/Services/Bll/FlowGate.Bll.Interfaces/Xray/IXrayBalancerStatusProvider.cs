using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Interfaces.Xray;

/// <summary>
///     Выбор балансировщиков основного Xray через <c>xray api bi</c>; если версия Xray этого не умеет, статус
///     <see cref="XrayBalancerSupport.Unsupported"/>, а не ошибка
/// </summary>
public interface IXrayBalancerStatusProvider
{
    /// <summary>
    ///     Возвращает выбор балансировщиков; результат кешируется на несколько секунд
    /// </summary>
    /// <param name="balancerTags">Теги балансировщиков из действующего конфига</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Статус; исключений из-за Xray не бросает</returns>
    Task<XrayBalancerStatus> GetAsync(IReadOnlyList<string> balancerTags, CancellationToken cancellationToken);
}
