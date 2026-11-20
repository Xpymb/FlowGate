using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Область одной операции обновления (задачи): все HTTP-запросы клиентов обновлений внутри неё идут одним путём,
///     выбранным в начале операции. Область привязана к асинхронному контексту, поэтому входить в неё нужно синхронным
///     <see cref="Enter"/> в теле операции
/// </summary>
public sealed class UpdateProxyScope : IDisposable
{
    private static readonly AsyncLocal<UpdateProxyScope?> CurrentScope = new();

    private readonly UpdateProxyScope? _parent;
    private bool _disposed;

    private UpdateProxyScope(UpdateVia via, Uri? proxyUri, UpdateProxyScope? parent)
    {
        Via = via;
        ProxyUri = proxyUri;
        _parent = parent;
    }

    /// <summary>
    ///     Область текущей операции или null
    /// </summary>
    public static UpdateProxyScope? Current => CurrentScope.Value is { _disposed: false } scope ? scope : null;

    /// <summary>
    ///     Выбранный путь
    /// </summary>
    public UpdateVia Via { get; }

    /// <summary>
    ///     Адрес прокси на всю операцию или null — запросы идут напрямую
    /// </summary>
    public Uri? ProxyUri { get; }

    /// <summary>
    ///     Входит в область
    /// </summary>
    /// <param name="via">Путь</param>
    /// <param name="proxyUri">Адрес, полученный <see cref="IUpdateProxyService.ResolveAsync(UpdateVia, CancellationToken)"/>, или null</param>
    /// <returns>Область; выход — <see cref="Dispose"/></returns>
    public static UpdateProxyScope Enter(UpdateVia via, Uri? proxyUri)
    {
        var scope = new UpdateProxyScope(via, proxyUri, CurrentScope.Value);
        CurrentScope.Value = scope;
        return scope;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (ReferenceEquals(CurrentScope.Value, this))
        {
            CurrentScope.Value = _parent;
        }
    }
}
