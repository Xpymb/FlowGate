using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Settings;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Settings;
using FlowGate.Contracts.UpdateProxy;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Servers;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.Settings;

/// <summary>
///     Настройки FlowGate: проверка, сохранение в <c>settings.json</c> и применение
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly ISettingsProvider _settings;
    private readonly IServerProvider _servers;
    private readonly IUpdateProxyService _updateProxy;
    private readonly IXrayConfigService _xrayConfig;
    private readonly ILogger<SettingsService> _logger;
    private readonly IntervalDefaults _defaults;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="SettingsService"/>
    /// </summary>
    /// <param name="settings">Хранилище настроек</param>
    /// <param name="servers">Хранилище серверов (проверка сервера прокси)</param>
    /// <param name="updateProxy">Прокси обновлений</param>
    /// <param name="xrayConfig">Конфигурация Xray (уровень логирования)</param>
    /// <param name="subscriptionOptions">Настройки подписок (интервал по умолчанию)</param>
    /// <param name="domainSourceOptions">Настройки источников (интервал по умолчанию)</param>
    /// <param name="xrayOptions">Настройки Xray (интервал проверки по умолчанию)</param>
    /// <param name="systemOptions">Настройки FlowGate (интервал проверки по умолчанию)</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public SettingsService(
        ISettingsProvider settings,
        IServerProvider servers,
        IUpdateProxyService updateProxy,
        IXrayConfigService xrayConfig,
        IOptions<SubscriptionOptions> subscriptionOptions,
        IOptions<DomainSourceOptions> domainSourceOptions,
        IOptions<XrayOptions> xrayOptions,
        IOptions<SystemOptions> systemOptions,
        ILogger<SettingsService> logger)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _servers = servers ?? throw new ArgumentNullException(nameof(servers));
        _updateProxy = updateProxy ?? throw new ArgumentNullException(nameof(updateProxy));
        _xrayConfig = xrayConfig ?? throw new ArgumentNullException(nameof(xrayConfig));
        ArgumentNullException.ThrowIfNull(subscriptionOptions);
        ArgumentNullException.ThrowIfNull(domainSourceOptions);
        ArgumentNullException.ThrowIfNull(xrayOptions);
        ArgumentNullException.ThrowIfNull(systemOptions);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _defaults = new IntervalDefaults(
            subscriptionOptions.Value.DefaultUpdateIntervalHours,
            domainSourceOptions.Value.RefreshIntervalHours,
            xrayOptions.Value.UpdateCheckIntervalHours,
            systemOptions.Value.UpdateCheckIntervalHours);
    }

    /// <inheritdoc />
    public async Task<SettingsDto> GetAsync(CancellationToken cancellationToken) =>
        ToDto(await _settings.GetAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<SettingsDto> UpdateAsync(SettingsSaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var errors = new List<string>();
        var logLevel = request.XrayLogLevel?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!XrayConfigModel.LogLevels.Contains(logLevel))
        {
            errors.Add($"Уровень логирования Xray: допустимы {string.Join(", ", XrayConfigModel.LogLevels)}");
        }

        var proxy = await ValidateProxyAsync(request.UpdateProxy, errors, cancellationToken);
        var intervals = ValidateIntervals(request.Intervals, errors);

        if (errors.Count > 0)
        {
            throw new RequestValidationException(errors.Count == 1 ? errors[0] : "Некорректные настройки", errors);
        }

        var before = await _settings.GetAsync(cancellationToken);
        var saved = await _settings.UpdateAsync(
            current => current with { XrayLogLevel = logLevel, UpdateProxy = proxy, Intervals = intervals },
            cancellationToken);

        _logger.LogInformation(
            "Настройки сохранены: логи Xray {LogLevel}, прокси обновлений {Proxy}",
            saved.XrayLogLevel,
            saved.UpdateProxy.Enabled ? $"через сервер {saved.UpdateProxy.ServerId}" : "выключен");

        if (!before.UpdateProxy.SameAs(saved.UpdateProxy))
        {
            await _updateProxy.InvalidateAsync(cancellationToken);
        }

        if (!string.Equals(before.XrayLogLevel, saved.XrayLogLevel, StringComparison.Ordinal))
        {
            await _xrayConfig.ApplyAsync(cancellationToken);
        }

        return ToDto(saved);
    }

    /// <inheritdoc />
    public async Task<UpdateProxyStatusDto> GetUpdateProxyStatusAsync(CancellationToken cancellationToken) =>
        SettingsMapper.ToDto(await _updateProxy.GetStatusAsync(cancellationToken));

    private async Task<UpdateProxySettings> ValidateProxyAsync(
        UpdateProxySaveRequest? request,
        List<string> errors,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            errors.Add("Не заданы настройки прокси обновлений");
            return UpdateProxySettings.Default;
        }

        var serverId = string.IsNullOrWhiteSpace(request.ServerId) ? null : request.ServerId.Trim();
        if (serverId is null && request.Enabled)
        {
            errors.Add("Выберите сервер для прокси обновлений");
        }
        else if (serverId is not null && await _servers.GetAsync(serverId, cancellationToken) is null)
        {
            errors.Add($"Сервер {serverId} не найден");
        }

        var raw = (request.ExtraDomains ?? []).Where(domain => !string.IsNullOrWhiteSpace(domain)).ToList();
        if (raw.Count > UpdateProxyDomains.MaxExtraDomains)
        {
            errors.Add($"Своих доменов не больше {UpdateProxyDomains.MaxExtraDomains}");
        }

        var domains = new List<string>();
        foreach (var domain in raw)
        {
            if (UpdateProxyDomains.Normalize(domain) is { } normalized)
            {
                domains.Add(normalized);
            }
            else
            {
                errors.Add($"«{domain.Trim()}» — не домен; допустимо example.com, domain:example.com, full:cdn.example.com");
            }
        }

        return new UpdateProxySettings
        {
            Enabled = request.Enabled,
            ServerId = serverId,
            ExtraDomains = domains.Distinct(StringComparer.Ordinal).ToList(),
        };
    }

    private static UpdateIntervalSettings ValidateIntervals(UpdateIntervalsSaveRequest? request, List<string> errors)
    {
        if (request is null)
        {
            return UpdateIntervalSettings.Default;
        }

        Check(request.SubscriptionDefaultHours, UpdateIntervalSettings.MaxSubscriptionHours, "Интервал обновления подписок");
        Check(request.DomainSourceRefreshHours, UpdateIntervalSettings.MaxHours, "Интервал обновления источников");
        Check(request.XrayUpdateCheckHours, UpdateIntervalSettings.MaxHours, "Интервал проверки версий Xray");
        Check(request.SystemUpdateCheckHours, UpdateIntervalSettings.MaxHours, "Интервал проверки версий FlowGate");

        return new UpdateIntervalSettings
        {
            SubscriptionDefaultHours = request.SubscriptionDefaultHours,
            DomainSourceRefreshHours = request.DomainSourceRefreshHours,
            XrayUpdateCheckHours = request.XrayUpdateCheckHours,
            SystemUpdateCheckHours = request.SystemUpdateCheckHours,
        };

        void Check(int? hours, int max, string name)
        {
            if (hours is { } value && (value < 1 || value > max))
            {
                errors.Add($"{name}: от 1 до {max} ч");
            }
        }
    }

    private SettingsDto ToDto(AppSettings settings) => SettingsMapper.ToDto(settings, _defaults);
}
