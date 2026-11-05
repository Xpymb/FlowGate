using FlowGate.Shared.Options;
using FlowGate.Shared.Options.Validation;

namespace FlowGate.Tests.Unit.Options;

/// <summary>
///     Тесты сгенерированных валидаторов опций
/// </summary>
public class OptionsValidatorTests
{
    /// <summary>
    ///     Значения по умолчанию проходят валидацию
    /// </summary>
    [Fact]
    public void Validate_DefaultKeeneticOptions_Succeeds()
    {
        var result = new KeeneticOptionsValidator().Validate(null, new KeeneticOptions());

        Assert.True(result.Succeeded);
    }

    /// <summary>
    ///     Таймаут вне диапазона и пустой адрес RCI отклоняются
    /// </summary>
    [Fact]
    public void Validate_InvalidKeeneticOptions_FailsWithBothProblems()
    {
        var options = new KeeneticOptions { RciBaseUrl = string.Empty, RequestTimeoutSeconds = 0 };

        var result = new KeeneticOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(nameof(KeeneticOptions.RciBaseUrl), result.FailureMessage);
        Assert.Contains(nameof(KeeneticOptions.RequestTimeoutSeconds), result.FailureMessage);
    }

    /// <summary>
    ///     Пустой каталог данных отклоняется
    /// </summary>
    [Fact]
    public void Validate_EmptyDataDirectory_Fails()
    {
        var result = new FlowGateOptionsValidator().Validate(null, new FlowGateOptions { DataDirectory = string.Empty });

        Assert.True(result.Failed);
    }
}
