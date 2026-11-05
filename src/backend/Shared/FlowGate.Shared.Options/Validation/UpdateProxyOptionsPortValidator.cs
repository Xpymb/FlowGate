using Microsoft.Extensions.Options;

namespace FlowGate.Shared.Options.Validation;

/// <summary>
///     Проверка, что порты прокси обновлений различаются и не пересекаются с диапазоном портов соединений
/// </summary>
public sealed class UpdateProxyOptionsPortValidator : IValidateOptions<UpdateProxyOptions>
{
    private readonly IOptions<ConnectionOptions> _connections;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="UpdateProxyOptionsPortValidator"/>
    /// </summary>
    /// <param name="connections">Настройки соединений (диапазон портов)</param>
    /// <exception cref="ArgumentNullException">Если connections равен null</exception>
    public UpdateProxyOptionsPortValidator(IOptions<ConnectionOptions> connections)
    {
        _connections = connections ?? throw new ArgumentNullException(nameof(connections));
    }

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, UpdateProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Port == options.UpgradePort)
        {
            return ValidateOptionsResult.Fail(
                $"{nameof(UpdateProxyOptions.Port)} и {nameof(UpdateProxyOptions.UpgradePort)} совпадают ({options.Port})");
        }

        var range = _connections.Value;
        var clash = new[] { options.Port, options.UpgradePort }
            .Where(port => port >= range.PortRangeStart && port <= range.PortRangeEnd)
            .ToList();

        return clash.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Порт прокси обновлений {clash[0]} попадает в диапазон соединений {range.PortRangeStart}–{range.PortRangeEnd}");
    }
}
