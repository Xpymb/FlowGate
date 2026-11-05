using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Источник модели без соединений. Заменяется источником из соединений на этапе 4
/// </summary>
public sealed class EmptyXrayConfigSource : IXrayConfigSource
{
    /// <inheritdoc />
    public Task<XrayConfigModel> GetModelAsync(string logLevel, CancellationToken cancellationToken) =>
        Task.FromResult(XrayConfigModel.Empty(logLevel));
}
