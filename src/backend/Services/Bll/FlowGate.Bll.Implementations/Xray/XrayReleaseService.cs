using Microsoft.Extensions.Options;

using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Релизы Xray с GitHub с кэшем на 10 минут
/// </summary>
public sealed class XrayReleaseService : IXrayReleaseService
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromMinutes(10);

    private readonly IGitHubReleasesClient _gitHub;
    private readonly TimeProvider _timeProvider;
    private readonly XrayOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<XrayRelease>? _cached;
    private DateTimeOffset _cachedAt;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayReleaseService"/>
    /// </summary>
    /// <param name="gitHub">Клиент релизов GitHub</param>
    /// <param name="options">Настройки Xray (репозиторий, архив, число релизов)</param>
    /// <param name="timeProvider">Источник времени</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayReleaseService(IGitHubReleasesClient gitHub, IOptions<XrayOptions> options, TimeProvider timeProvider)
    {
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public XrayUpdateCheck? LastCheck { get; private set; }

    /// <inheritdoc />
    public async Task<IReadOnlyList<XrayRelease>> GetReleasesAsync(bool refresh, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = _timeProvider.GetUtcNow();
            if (!refresh && _cached is not null && now - _cachedAt < CacheLifetime)
            {
                return _cached;
            }

            var releases = await _gitHub.GetReleasesAsync(_options.Repository, _options.ReleasesCount, cancellationToken);
            _cached = releases
                .Select(release => XrayRelease.TryCreate(
                    release.TagName,
                    release.PublishedAt,
                    release.Prerelease,
                    release.Draft,
                    release.Assets.Select(asset => (asset.Name, asset.BrowserDownloadUrl, asset.Size)).ToList(),
                    _options.ReleaseAsset))
                .OfType<XrayRelease>()
                .OrderByDescending(release => release.Version)
                .ToList();
            _cachedAt = now;
            LastCheck = new XrayUpdateCheck(LatestStable(_cached)?.Version, now);
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<XrayRelease?> FindAsync(XrayVersion? version, CancellationToken cancellationToken)
    {
        var releases = await GetReleasesAsync(refresh: false, cancellationToken);
        var release = Find(releases, version);
        if (release is not null)
        {
            return release;
        }

        releases = await GetReleasesAsync(refresh: true, cancellationToken);
        return Find(releases, version);
    }

    /// <inheritdoc />
    public async Task<XrayUpdateCheck> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        await GetReleasesAsync(refresh: true, cancellationToken);
        return LastCheck!;
    }

    private static XrayRelease? Find(IReadOnlyList<XrayRelease> releases, XrayVersion? version) =>
        version is null
            ? LatestStable(releases)
            : releases.FirstOrDefault(release => release.Version == version);

    private static XrayRelease? LatestStable(IReadOnlyList<XrayRelease> releases) =>
        releases.FirstOrDefault(release => !release.IsPrerelease);
}
