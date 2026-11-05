using System.Text.Json;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Contracts.Keenetic;
using FlowGate.Exceptions;
using FlowGate.Shared.Keenetic;
using FlowGate.SharedContracts.Dto.Keenetic;

namespace FlowGate.Bll.Implementations.Keenetic;

/// <summary>
///     Сведения о роутере по ответу RCI <c>show/version</c>
/// </summary>
public sealed class KeeneticSystemService : IKeeneticSystemService
{
    private const string VersionPath = "show/version";

    private readonly IKeeneticRciClient _rciClient;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="KeeneticSystemService"/>
    /// </summary>
    /// <param name="rciClient">Клиент RCI</param>
    /// <exception cref="ArgumentNullException">Если rciClient равен null</exception>
    public KeeneticSystemService(IKeeneticRciClient rciClient)
    {
        _rciClient = rciClient ?? throw new ArgumentNullException(nameof(rciClient));
    }

    /// <inheritdoc />
    public async Task<KeeneticSystemInfoDto> GetSystemInfoAsync(CancellationToken cancellationToken)
    {
        var version = await _rciClient.GetAsync(VersionPath, cancellationToken);
        return Parse(version).ToDto();
    }

    private static KeeneticSystemInfo Parse(JsonElement version)
    {
        if (version.ValueKind != JsonValueKind.Object)
        {
            throw new KeeneticUnavailableException($"RCI {VersionPath} вернул не объект");
        }

        var release = ReadString(version, "release");
        if (string.IsNullOrEmpty(release))
        {
            throw new KeeneticUnavailableException($"RCI {VersionPath} не содержит release");
        }

        var components = version.TryGetProperty("ndw", out var ndw)
            ? ReadString(ndw, "components")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];

        return new KeeneticSystemInfo(
            ReadString(version, "model"),
            ReadString(version, "hw_id"),
            release,
            ReadString(version, "title"),
            ReadString(version, "arch"),
            components);
    }

    private static string ReadString(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
