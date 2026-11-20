namespace FlowGate.Bll.Interfaces.Servers;

/// <summary>
///     HTTP-запрос через локальный socks-прокси временного Xray
/// </summary>
public interface IOutboundProbe
{
    /// <summary>
    ///     Запрашивает адрес через socks5-прокси <c>127.0.0.1:port</c> и меряет время до заголовков ответа
    /// </summary>
    /// <param name="port">Порт socks-inbound</param>
    /// <param name="url">Адрес проверки</param>
    /// <param name="timeout">Таймаут запроса</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Время запроса</returns>
    /// <exception cref="HttpRequestException">Если запрос через прокси не удался</exception>
    /// <exception cref="TimeoutException">Если ответа нет за <paramref name="timeout"/></exception>
    Task<TimeSpan> ProbeAsync(int port, Uri url, TimeSpan timeout, CancellationToken cancellationToken);
}
