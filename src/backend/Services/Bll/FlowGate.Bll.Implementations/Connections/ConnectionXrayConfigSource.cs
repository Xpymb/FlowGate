using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Connections;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Connections;

/// <summary>
///     Источник модели Xray из сохранённых соединений и серверов (<see cref="ConnectionXrayModel"/>)
/// </summary>
public sealed class ConnectionXrayConfigSource : IXrayConfigSource
{
    private readonly IConnectionProvider _connections;
    private readonly IServerProvider _servers;
    private readonly ConnectionOptions _options;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ConnectionXrayConfigSource"/>
    /// </summary>
    /// <param name="connections">Хранилище соединений</param>
    /// <param name="servers">Хранилище серверов</param>
    /// <param name="options">Настройки соединений</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public ConnectionXrayConfigSource(IConnectionProvider connections, IServerProvider servers, IOptions<ConnectionOptions> options)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<XrayConfigModel> GetModelAsync(string logLevel, CancellationToken cancellationToken)
    {
        var connections = await _connections.GetAllAsync(cancellationToken);
        if (connections.Count == 0)
        {
            return XrayConfigModel.Empty(logLevel);
        }

        var servers = await _servers.GetAllAsync(cancellationToken);
        return ConnectionXrayModel.Build(logLevel, connections, servers, _options.ListenAddress, _options.WireguardMtu);
    }
}
