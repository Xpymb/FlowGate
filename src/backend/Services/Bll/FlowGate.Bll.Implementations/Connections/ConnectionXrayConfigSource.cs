using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Connections;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Источник модели Xray из сохранённых соединений и серверов (<see cref="ConnectionXrayModel"/>) плюс inbound
///     прокси обновлений (<see cref="UpdateProxyXrayModel"/>), если для него выбран существующий сервер
/// </summary>
public sealed class ConnectionXrayConfigSource : IXrayConfigSource
{
    private readonly IConnectionProvider _connections;
    private readonly IServerProvider _servers;
    private readonly ISettingsProvider _settings;
    private readonly ConnectionOptions _options;
    private readonly UpdateProxyOptions _updateProxyOptions;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionXrayConfigSource"/>
    /// </summary>
    /// <param name="connections">Хранилище соединений</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="settings">Пользовательские настройки (сервер прокси обновлений)</param>
    /// <param name="options">Настройки соединений</param>
    /// <param name="updateProxyOptions">Настройки прокси обновлений (порт)</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public ConnectionXrayConfigSource(
        IConnectionProvider connections,
        IServerProvider servers,
        ISettingsProvider settings,
        IOptions<ConnectionOptions> options,
        IOptions<UpdateProxyOptions> updateProxyOptions)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(updateProxyOptions);
        _options = options.Value;
        _updateProxyOptions = updateProxyOptions.Value;
    }

    /// <inheritdoc />
    public async Task<XrayConfigModel> GetModelAsync(string logLevel, CancellationToken cancellationToken)
    {
        var connections = await _connections.GetAllAsync(cancellationToken);
        var settings = await _settings.GetAsync(cancellationToken);
        var proxyServerId = settings.UpdateProxy.ServerId;

        if (connections.Count == 0 && string.IsNullOrWhiteSpace(proxyServerId))
        {
            return XrayConfigModel.Empty(logLevel);
        }

        var servers = await _servers.GetAllAsync(cancellationToken);
        var model = connections.Count == 0
            ? XrayConfigModel.Empty(logLevel)
            : ConnectionXrayModel.Build(logLevel, connections, servers, _options.ListenAddress, _options.WireguardMtu);

        var proxyServer = servers.FirstOrDefault(server => string.Equals(server.Id, proxyServerId, StringComparison.Ordinal));
        return proxyServer is null
            ? model
            : UpdateProxyXrayModel.Append(model, proxyServer.Outbound, _updateProxyOptions.Port);
    }
}
