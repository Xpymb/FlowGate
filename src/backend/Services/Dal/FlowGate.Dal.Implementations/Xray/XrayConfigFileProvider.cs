using Microsoft.Extensions.Options;

using FlowGate.Dal.Interfaces.Xray;
using FlowGate.Shared.Dal.Files;
using FlowGate.Shared.Options;

namespace FlowGate.Dal.Implementations.Xray;

/// <summary>
///     Файл конфигурации Xray в каталоге данных
/// </summary>
public sealed class XrayConfigFileProvider : IXrayConfigFileProvider
{
    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayConfigFileProvider"/>
    /// </summary>
    /// <param name="flowGateOptions">Общие настройки (каталог данных)</param>
    /// <param name="xrayOptions">Настройки Xray (имя файла)</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayConfigFileProvider(IOptions<FlowGateOptions> flowGateOptions, IOptions<XrayOptions> xrayOptions)
    {
        ArgumentNullException.ThrowIfNull(flowGateOptions);
        ArgumentNullException.ThrowIfNull(xrayOptions);
        ConfigPath = Path.Combine(Path.GetFullPath(flowGateOptions.Value.DataDirectory), xrayOptions.Value.ConfigFileName);
    }

    /// <inheritdoc />
    public string ConfigPath { get; }

    /// <inheritdoc />
    public async Task<byte[]?> ReadAsync(CancellationToken cancellationToken) =>
        File.Exists(ConfigPath) ? await File.ReadAllBytesAsync(ConfigPath, cancellationToken) : null;

    /// <inheritdoc />
    public Task<string> WriteCandidateAsync(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        AtomicFile.WriteTemporaryAsync(
            ConfigPath,
            (stream, token) => stream.WriteAsync(content, token).AsTask(),
            cancellationToken);

    /// <inheritdoc />
    public void Commit(string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        File.Move(candidatePath, ConfigPath, overwrite: true);
    }

    /// <inheritdoc />
    public void Discard(string candidatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(candidatePath);
        AtomicFile.DeleteIfExists(candidatePath);
    }
}
