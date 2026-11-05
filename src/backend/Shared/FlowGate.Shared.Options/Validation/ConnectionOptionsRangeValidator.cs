using Microsoft.Extensions.Options;

namespace FlowGate.Shared.Options.Validation;

/// <summary>
///     Проверка согласованности <see cref="ConnectionOptions"/>, которую нельзя выразить атрибутами
/// </summary>
public sealed class ConnectionOptionsRangeValidator : IValidateOptions<ConnectionOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ConnectionOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.PortRangeStart <= options.PortRangeEnd
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{nameof(ConnectionOptions.PortRangeStart)} ({options.PortRangeStart}) больше {nameof(ConnectionOptions.PortRangeEnd)} ({options.PortRangeEnd})");
    }
}
