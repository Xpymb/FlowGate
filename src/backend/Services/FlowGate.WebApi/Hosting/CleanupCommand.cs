using FlowGate.Bll.Interfaces.Maintenance;

namespace FlowGate.WebApi.Hosting;

/// <summary>
///     Команда <c>flowgate cleanup</c>: вызывается из <c>prerm</c> пакета при удалении
/// </summary>
public static class CleanupCommand
{
    /// <summary>
    ///     Выполняет очистку Keenetic и печатает итог
    /// </summary>
    /// <param name="services">Контейнер собранного, но не запущенного хоста</param>
    /// <param name="dryRun">Только показать команды</param>
    /// <param name="output">Куда печатать</param>
    /// <param name="cancellationToken">Токен отмены</param>
    /// <returns>Код выхода: 0 — успех, 1 — были ошибки</returns>
    /// <exception cref="ArgumentNullException">Если services или output равен null</exception>
    public static async Task<int> RunAsync(
        IServiceProvider services,
        bool dryRun,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(output);

        await using var scope = services.CreateAsyncScope();
        var cleanup = scope.ServiceProvider.GetRequiredService<ICleanupService>();
        var report = await cleanup.RunAsync(dryRun, cancellationToken);

        if (dryRun)
        {
            await output.WriteLineAsync(report.Commands.Count == 0 ? "Удалять нечего" : "Команды NDMS:");
            foreach (var command in report.Commands)
            {
                await output.WriteLineAsync("  " + command);
            }
        }

        await output.WriteLineAsync(
            $"{(dryRun ? "Будет удалено" : "Удалено")}: правил {report.Routes}, групп {report.Groups}, интерфейсов {report.Interfaces}");
        foreach (var error in report.Errors)
        {
            await output.WriteLineAsync("Ошибка: " + error);
        }

        return report.Succeeded ? 0 : 1;
    }
}
