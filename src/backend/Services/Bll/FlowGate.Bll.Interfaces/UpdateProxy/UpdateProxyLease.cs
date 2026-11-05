namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Аренда прокси обновлений на время одного запроса; отпускается через <see cref="Dispose"/> (повторный вызов ничего не делает)
/// </summary>
public sealed class UpdateProxyLease : IDisposable
{
    private Action? _release;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyLease"/>
    /// </summary>
    /// <param name="proxyUri">Адрес HTTP-прокси</param>
    /// <param name="release">Вызывается один раз при освобождении</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public UpdateProxyLease(Uri proxyUri, Action release)
    {
        ProxyUri = proxyUri ?? throw new ArgumentNullException(nameof(proxyUri));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    /// <summary>
    ///     Адрес HTTP-прокси, например <c>http://127.0.0.1:10790</c>
    /// </summary>
    public Uri ProxyUri { get; }

    /// <inheritdoc />
    public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
}
