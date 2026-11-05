using Microsoft.Extensions.Logging;

using FlowGate.Bll.Interfaces.Keenetic;
using FlowGate.Bll.Interfaces.Maintenance;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Connections;
using FlowGate.Contracts.DnsRoutes;
using FlowGate.Contracts.Maintenance;
using FlowGate.Dal.Interfaces.DnsRoutes;

namespace FlowGate.Bll.Implementations.Maintenance;

/// <summary>
///     Очистка Keenetic от объектов FlowGate: сначала xray, затем DNS-маршруты и группы, затем интерфейсы
/// </summary>
public sealed class CleanupService : ICleanupService
{
    private const string DeleteRulePrefix = "no dns-proxy route ";
    private const string DeleteGroupPrefix = "no object-group fqdn ";

    private readonly IXrayProcessService _xray;
    private readonly IKeeneticDnsService _dns;
    private readonly IKeeneticInterfaceService _interfaces;
    private readonly IDnsRouteProvider _routes;
    private readonly ILogger<CleanupService> _logger;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="CleanupService"/>
    /// </summary>
    /// <param name="xray">Процесс Xray</param>
    /// <param name="dns">Группы и DNS-маршруты Keenetic</param>
    /// <param name="interfaces">Интерфейсы Keenetic</param>
    /// <param name="routes">Хранилище маршрутов</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public CleanupService(
        IXrayProcessService xray,
        IKeeneticDnsService dns,
        IKeeneticInterfaceService interfaces,
        IDnsRouteProvider routes,
        ILogger<CleanupService> logger)
    {
        _xray = xray ?? throw new ArgumentNullException(nameof(xray));
        _dns = dns ?? throw new ArgumentNullException(nameof(dns));
        _interfaces = interfaces ?? throw new ArgumentNullException(nameof(interfaces));
        _routes = routes ?? throw new ArgumentNullException(nameof(routes));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<CleanupReport> RunAsync(bool dryRun, CancellationToken cancellationToken)
    {
        var commands = new List<string>();
        var errors = new List<string>();

        if (!dryRun)
        {
            await RunStepAsync("остановка xray", errors, () => _xray.KillOrphanAsync(cancellationToken));
        }

        var dnsCommands = new List<string>();
        await RunStepAsync("DNS-маршруты и группы", errors, async () =>
        {
            var state = await _dns.GetStateAsync(cancellationToken);
            dnsCommands.AddRange(DnsRoutePlanner.RemoveAll(state));
            if (!dryRun && dnsCommands.Count > 0)
            {
                await _dns.ExecuteAsync("flowgate-cleanup-dns", dnsCommands, report: null, cancellationToken);
            }

            if (!dryRun)
            {
                await ResetProvisioningAsync(cancellationToken);
            }
        });
        commands.AddRange(dnsCommands);

        var interfaceCommands = new List<string>();
        await RunStepAsync("интерфейсы", errors, async () =>
        {
            var all = await _interfaces.GetAllAsync(cancellationToken);
            interfaceCommands.AddRange(all.Values
                .Where(state => state.Description.StartsWith(Connection.DescriptionPrefix, StringComparison.Ordinal))
                .Select(state => state.Name)
                .Order(StringComparer.Ordinal)
                .Select(KeeneticInterfaceCommands.Delete));
            if (!dryRun && interfaceCommands.Count > 0)
            {
                await _interfaces.ExecuteAsync("flowgate-cleanup-interfaces", interfaceCommands, ignoreMissing: true, cancellationToken);
            }
        });
        commands.AddRange(interfaceCommands);

        var report = new CleanupReport(
            commands,
            dnsCommands.Count(command => command.StartsWith(DeleteRulePrefix, StringComparison.Ordinal)),
            dnsCommands.Count(command => command.StartsWith(DeleteGroupPrefix, StringComparison.Ordinal)),
            interfaceCommands.Count,
            errors);

        if (!dryRun)
        {
            _logger.LogInformation(
                "Keenetic очищен от объектов FlowGate: правил {Routes}, групп {Groups}, интерфейсов {Interfaces}, ошибок {Errors}",
                report.Routes,
                report.Groups,
                report.Interfaces,
                errors.Count);
        }

        return report;
    }

    private async Task ResetProvisioningAsync(CancellationToken cancellationToken)
    {
        var routes = await _routes.GetAllAsync(cancellationToken);
        foreach (var route in routes.Where(route => route.Provisioned || route.AppliedInterface is not null))
        {
            await _routes.UpdateAsync(
                route.Id,
                current => current with { Provisioned = false, AppliedInterface = null },
                cancellationToken);
        }
    }

    private async Task RunStepAsync(string step, List<string> errors, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Очистка Keenetic: шаг «{Step}» не выполнен", step);
            errors.Add($"{step}: {ex.Message}");
        }
    }
}
