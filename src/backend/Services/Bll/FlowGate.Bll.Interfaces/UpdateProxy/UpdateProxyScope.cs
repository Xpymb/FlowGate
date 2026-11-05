using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Interfaces.UpdateProxy;

/// <summary>
///     Область одной операции обновления (задачи): все HTTP-запросы клиентов обновлений внутри неё идут выбранным путём
///     и через одну аренду прокси. Область привязана к асинхронному контексту, поэтому входить в неё нужно синхронным
///     <see cref="Enter"/> в теле операции
/// </summary>
public sealed class UpdateProxyScope : IDisposable
{
    private static readonly AsyncLocal<UpdateProxyScope?> CurrentScope = new();

    private readonly UpdateProxyScope? _parent;
    private bool _disposed;

    private UpdateProxyScope(UpdateVia via, UpdateProxyLease? lease, UpdateProxyScope? parent)
    {
        Via = via;
        Lease = lease;
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
    ///     Аренда прокси на всю операцию или null — запросы идут напрямую
    /// </summary>
    public UpdateProxyLease? Lease { get; }

    /// <summary>
    ///     Входит в область; аренда отпускается при выходе
    /// </summary>
    /// <param name="via">Путь</param>
    /// <param name="lease">Аренда, полученная <see cref="IUpdateProxyService.AcquireAsync(UpdateVia, CancellationToken)"/>, или null</param>
    /// <returns>Область; выход — <see cref="Dispose"/></returns>
    public static UpdateProxyScope Enter(UpdateVia via, UpdateProxyLease? lease)
    {
        var scope = new UpdateProxyScope(via, lease, CurrentScope.Value);
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

        Lease?.Dispose();
    }
}
