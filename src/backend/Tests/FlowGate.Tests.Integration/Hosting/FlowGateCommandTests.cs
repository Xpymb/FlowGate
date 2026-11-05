using FlowGate.WebApi.Hosting;

namespace FlowGate.Tests.Integration.Hosting;

/// <summary>
///     Тесты разбора командной строки <see cref="FlowGateCommand"/>
/// </summary>
public sealed class FlowGateCommandTests
{
    /// <summary>
    ///     Без команды или с опциями — веб-сервер, аргументы уходят хосту как есть
    /// </summary>
    [Fact]
    public void Parse_NoCommand_Serve()
    {
        var empty = FlowGateCommand.Parse([]);
        var options = FlowGateCommand.Parse(["--urls", "http://0.0.0.0:8090"]);

        Assert.Equal(FlowGateCommandKind.Serve, empty.Kind);
        Assert.Equal(FlowGateCommandKind.Serve, options.Kind);
        Assert.Equal(["--urls", "http://0.0.0.0:8090"], options.HostArguments);
    }

    /// <summary>
    ///     <c>cleanup --dry-run</c>: пробный режим, переопределения конфигурации уходят хосту без самой команды
    /// </summary>
    [Fact]
    public void Parse_CleanupDryRun_KeepsConfigurationOverrides()
    {
        var command = FlowGateCommand.Parse(["cleanup", "--FlowGate:DataDirectory=/opt/etc/flowgate", "--dry-run"]);

        Assert.Equal(FlowGateCommandKind.Cleanup, command.Kind);
        Assert.True(command.DryRun);
        Assert.Equal(["--FlowGate:DataDirectory=/opt/etc/flowgate"], command.HostArguments);
        Assert.False(FlowGateCommand.Parse(["cleanup"]).DryRun);
    }

    /// <summary>
    ///     Справка и неизвестная команда
    /// </summary>
    [Fact]
    public void Parse_HelpAndUnknown_Recognized()
    {
        Assert.Equal(FlowGateCommandKind.Help, FlowGateCommand.Parse(["help"]).Kind);
        var unknown = FlowGateCommand.Parse(["purge"]);
        Assert.Equal(FlowGateCommandKind.Invalid, unknown.Kind);
        Assert.Contains("purge", unknown.Error);
    }
}
