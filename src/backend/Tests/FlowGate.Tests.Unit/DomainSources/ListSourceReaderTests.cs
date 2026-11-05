using FlowGate.Bll.Implementations.DomainSources.Readers;
using FlowGate.Contracts.DomainLists;
using FlowGate.Contracts.DomainSources;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.DomainSources;

using DatBuilder = FlowGate.Tests.Common.DomainSources.DatFileBuilder;

namespace FlowGate.Tests.Unit.DomainSources;

/// <summary>
///     Тесты читателей geosite.dat, geoip.dat и архива репозитория на файлах, собранных в тесте
/// </summary>
public sealed class ListSourceReaderTests : IDisposable
{
    private static readonly DomainSource Source = DomainSource.BuiltInSources[0];

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="ListSourceReaderTests"/>
    /// </summary>
    public ListSourceReaderTests()
    {
        Directory.CreateDirectory(_directory);
    }

    /// <summary>
    ///     Каталог geosite: имена в нижнем регистре, счётчики и атрибуты
    /// </summary>
    [Fact]
    public async Task Geosite_BuildCatalog_ReadsNamesCountsAndAttributes()
    {
        var path = WriteGeosite();

        var catalog = await new GeositeDatReader().BuildCatalogAsync(Source, path, Token);

        Assert.Equal(["nintendo", "youtube"], catalog.Lists.Select(list => list.Name));
        var youtube = catalog.Lists[1];
        Assert.Equal((3, 2), (youtube.Domains, youtube.Skipped));
        Assert.Equal(["ads"], youtube.Attributes);
        Assert.Equal(64, catalog.Sha256.Length);
    }

    /// <summary>
    ///     Список geosite с фильтром: Domain и Full уходят доменами, keyword и regexp пропускаются
    /// </summary>
    [Fact]
    public async Task Geosite_ReadListWithFilter_AppliesAttributes()
    {
        var path = WriteGeosite();
        var reader = new GeositeDatReader();

        var all = await reader.ReadListAsync(Source, path, "YouTube", null, Token);
        var withoutAds = await reader.ReadListAsync(Source, path, "youtube", "!ads", Token);

        Assert.Equal(["youtube.com", "www.youtube.com", "ytimg.com"], all.Entries);
        Assert.Equal((1, 1), (all.SkippedKeyword, all.SkippedRegexp));
        Assert.Equal(["youtube.com", "www.youtube.com"], withoutAds.Entries);
    }

    /// <summary>
    ///     geoip: подсети нормализуются, фильтр неприменим, отсутствующий список — ошибка
    /// </summary>
    [Fact]
    public async Task Geoip_ReadList_NormalizesAndRejectsFilter()
    {
        var path = Path.Combine(_directory, "geoip.data");
        await File.WriteAllBytesAsync(path, DatBuilder.Geoip(("TELEGRAM", ["91.108.56.1/22", "2001:67c:4e8::/48", "149.154.167.51/32"])), Token);
        var reader = new GeoipDatReader();

        var catalog = await reader.BuildCatalogAsync(Source, path, Token);
        var content = await reader.ReadListAsync(Source, path, "telegram", null, Token);

        Assert.Equal((0, 2, 1), (catalog.Lists[0].Domains, catalog.Lists[0].Ipv4, catalog.Lists[0].Ipv6));
        Assert.Equal(["91.108.56.0/22", "2001:67c:4e8::/48", "149.154.167.51"], content.Entries);
        await Assert.ThrowsAsync<DomainListException>(() => reader.ReadListAsync(Source, path, "telegram", "ads", Token));
        await Assert.ThrowsAsync<DomainListException>(() => reader.ReadListAsync(Source, path, "missing", null, Token));
    }

    /// <summary>
    ///     Обрезанный файл — ошибка источника, а не падение
    /// </summary>
    [Fact]
    public async Task Geosite_TruncatedFile_ThrowsDomainListException()
    {
        var bytes = DatBuilder.Geosite(("youtube", [(DatBuilder.Domain, "youtube.com", [])]));
        var path = Path.Combine(_directory, "broken.data");
        await File.WriteAllBytesAsync(path, bytes[..^3], Token);

        await Assert.ThrowsAsync<DomainListException>(() => new GeositeDatReader().BuildCatalogAsync(Source, path, Token));
    }

    /// <summary>
    ///     Репозиторий: берутся только файлы прямо в data/, .txt отбрасывается, include работает, ошибки строк в каталоге
    /// </summary>
    [Fact]
    public async Task Repository_BuildCatalogAndRead_HandlesLayoutAndErrors()
    {
        var path = Path.Combine(_directory, "repo.data");
        await File.WriteAllBytesAsync(
            path,
            RepositoryArchive.Create(
                "owner-lists-abc1234",
                new Dictionary<string, string>
                {
                    ["data/telegram.txt"] = "telegram.org\ninclude:telegram-ip\n",
                    ["data/telegram-ip"] = "91.108.4.0/22\n2001:b28:f23d::/48\n",
                    ["data/broken"] = "ok.com\nпример.рф\n",
                    ["data/loop"] = "include:loop\n",
                    ["data/nested/ignored"] = "ignored.com\n",
                    ["README.md"] = "# lists\n",
                }),
            Token);
        var reader = new GitHubRepositoryReader(Microsoft.Extensions.Options.Options.Create(new DomainSourceOptions()));

        var catalog = await reader.BuildCatalogAsync(Source, path, Token);
        var telegram = await reader.ReadListAsync(Source, path, "telegram", null, Token);

        Assert.Equal(["broken", "telegram", "telegram-ip"], catalog.Lists.Select(list => list.Name));
        Assert.Contains(catalog.Errors, error => error.StartsWith("data/broken: строка 2", StringComparison.Ordinal));
        Assert.Contains(catalog.Errors, error => error.StartsWith("loop:", StringComparison.Ordinal) && error.Contains("цикл"));
        Assert.Equal(["telegram.org", "91.108.4.0/22", "2001:b28:f23d::/48"], telegram.Entries);
    }

    /// <summary>
    ///     Списки репозитория больше лимита — ошибка
    /// </summary>
    [Fact]
    public async Task Repository_TooLarge_Throws()
    {
        var path = Path.Combine(_directory, "big.data");
        await File.WriteAllBytesAsync(path, RepositoryArchive.Create("o-r-1", new Dictionary<string, string> { ["data/big"] = new string('a', 4096) }), Token);
        var reader = new GitHubRepositoryReader(Microsoft.Extensions.Options.Options.Create(new DomainSourceOptions { MaxRepositoryBytes = 1024 }));

        await Assert.ThrowsAsync<DomainListException>(() => reader.BuildCatalogAsync(Source, path, Token));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private string WriteGeosite()
    {
        var path = Path.Combine(_directory, "geosite.data");
        File.WriteAllBytes(
            path,
            DatBuilder.Geosite(
                ("YOUTUBE",
                [
                    (DatBuilder.Domain, "youtube.com", []),
                    (DatBuilder.Full, "www.youtube.com", []),
                    (DatBuilder.Domain, "ytimg.com", ["ads"]),
                    (DatBuilder.Keyword, "youtube", []),
                    (DatBuilder.Regex, "^yt[0-9]+\\.com$", []),
                ]),
                ("nintendo", [(DatBuilder.Domain, "nintendo.com", [])])));
        return path;
    }
}
