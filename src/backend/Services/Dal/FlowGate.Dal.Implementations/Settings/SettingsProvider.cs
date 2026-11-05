using FlowGate.Contracts.Settings;
using FlowGate.Dal.Implementations.Serialization;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Shared.Dal.Json;

namespace FlowGate.Dal.Implementations.Settings;

/// <summary>
///     Пользовательские настройки в <c>settings.json</c>
/// </summary>
public sealed class SettingsProvider : ISettingsProvider
{
    /// <summary>
    ///     Имя файла настроек
    /// </summary>
    public const string FileName = "settings.json";

    private readonly IJsonFileStore<AppSettings> _store;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SettingsProvider"/>
    /// </summary>
    /// <param name="storeFactory">Фабрика хранилищ JSON-файлов</param>
    /// <exception cref="ArgumentNullException">Если storeFactory равен null</exception>
    public SettingsProvider(IJsonFileStoreFactory storeFactory)
    {
        ArgumentNullException.ThrowIfNull(storeFactory);
        _store = storeFactory.Create(FileName, DalJsonContext.Default.AppSettings, () => AppSettings.Default);
    }

    /// <inheritdoc />
    public Task<AppSettings> GetAsync(CancellationToken cancellationToken) => _store.ReadAsync(cancellationToken);

    /// <inheritdoc />
    public Task<AppSettings> UpdateAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken) =>
        _store.UpdateAsync(update, cancellationToken);
}
