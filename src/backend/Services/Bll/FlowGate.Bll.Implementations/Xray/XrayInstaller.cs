using System.ComponentModel;
using System.IO.Compression;
using System.Security.Cryptography;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.UpdateProxy;
using FlowGate.Bll.Interfaces.UpdateProxy;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Xray;
using FlowGate.Dal.Interfaces.Settings;
using FlowGate.Exceptions;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;
using FlowGate.SharedContracts.Dto.Settings;

namespace FlowGate.Bll.Implementations.Xray;

/// <summary>
///     Установка Xray: скачать архив и <c>.dgst</c>, сверить SHA-256, извлечь <c>xray</c>, проверить версию и конфигурацию,
///     подменить бинарник под блокировкой процесса, при неудачном запуске вернуть прежний
/// </summary>
public sealed class XrayInstaller : IXrayInstaller
{
    /// <summary>
    ///     Вид фоновой задачи установки
    /// </summary>
    public const string JobKind = "xray-install";

    private const string BinaryEntryName = "xray";
    private const long MaxArchiveBytes = 200L * 1024 * 1024;
    private const long MaxDigestBytes = 64 * 1024;
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(15);

    private readonly IXrayReleaseService _releases;
    private readonly IGitHubReleasesClient _gitHub;
    private readonly IProcessRunner _processRunner;
    private readonly IXrayProcessService _process;
    private readonly IXrayVersionProvider _versionProvider;
    private readonly IXrayConfigService _configService;
    private readonly ISettingsProvider _settings;
    private readonly IJobRunner _jobs;
    private readonly IUpdateProxyService _updateProxy;
    private readonly ILogger<XrayInstaller> _logger;
    private readonly XrayOptions _options;
    private readonly string _binaryPath;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayInstaller"/>
    /// </summary>
    /// <param name="releases">Релизы Xray</param>
    /// <param name="gitHub">Скачивание файлов</param>
    /// <param name="processRunner">Запуск <c>xray version</c></param>
    /// <param name="process">Процесс Xray</param>
    /// <param name="versionProvider">Установленная версия</param>
    /// <param name="configService">Конфигурация Xray</param>
    /// <param name="settings">Пользовательские настройки (автозапуск)</param>
    /// <param name="jobs">Фоновые задачи</param>
    /// <param name="updateProxy">Прокси обновлений (загрузка архива)</param>
    /// <param name="options">Настройки Xray</param>
    /// <param name="logger">Логгер</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public XrayInstaller(
        IXrayReleaseService releases,
        IGitHubReleasesClient gitHub,
        IProcessRunner processRunner,
        IXrayProcessService process,
        IXrayVersionProvider versionProvider,
        IXrayConfigService configService,
        ISettingsProvider settings,
        IJobRunner jobs,
        IUpdateProxyService updateProxy,
        IOptions<XrayOptions> options,
        ILogger<XrayInstaller> logger)
    {
        _releases = releases ?? throw new ArgumentNullException(nameof(releases));
        _gitHub = gitHub ?? throw new ArgumentNullException(nameof(gitHub));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _process = process ?? throw new ArgumentNullException(nameof(process));
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _configService = configService ?? throw new ArgumentNullException(nameof(configService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _updateProxy = updateProxy ?? throw new ArgumentNullException(nameof(updateProxy));
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _binaryPath = Path.GetFullPath(_options.BinaryPath);
    }

    /// <inheritdoc />
    public async Task<JobSnapshot> StartInstallAsync(XrayVersion? version, UpdateVia via, CancellationToken cancellationToken)
    {
        var release = await _releases.FindAsync(version, cancellationToken)
            ?? throw new EntityNotFoundException("Релиз Xray", version?.ToString() ?? "последний стабильный");

        return _jobs.Start(JobKind, $"Установка Xray {release.Version}", (context, token) => UpdateRoute.RunAsync(_updateProxy, via, context, () => InstallAsync(release, context, token), token));
    }

    private async Task InstallAsync(XrayRelease release, IJobContext context, CancellationToken cancellationToken)
    {
        var workDirectory = Path.Combine(Path.GetFullPath(_options.WorkDirectory), $"install-{context.JobId:N}");
        try
        {
            context.Report("Проверка свободного места");
            EnsureFreeSpace(_options.WorkDirectory);
            EnsureFreeSpace(Path.GetDirectoryName(_binaryPath)!);
            Directory.CreateDirectory(workDirectory);

            context.Report($"Скачивание {Path.GetFileName(release.ArchiveUrl.AbsolutePath)} ({release.ArchiveSize / 1024.0 / 1024.0:0.0} МиБ)");
            var archivePath = Path.Combine(workDirectory, "xray.zip");
            var digestPath = Path.Combine(workDirectory, "xray.zip.dgst");
            await _gitHub.DownloadAsync(release.ArchiveUrl, archivePath, MaxArchiveBytes, cancellationToken);
            await _gitHub.DownloadAsync(release.DigestUrl, digestPath, MaxDigestBytes, cancellationToken);

            context.Report("Проверка контрольной суммы SHA-256");
            await VerifyDigestAsync(archivePath, digestPath, cancellationToken);

            context.Report("Распаковка");
            var candidatePath = ExtractBinary(archivePath, workDirectory);

            context.Report("Проверка версии");
            await VerifyVersionAsync(candidatePath, release.Version, cancellationToken);

            context.Report("Проверка конфигурации новой версией");
            await _configService.WriteAsync(candidatePath, cancellationToken);

            var previous = await _versionProvider.GetInstalledVersionAsync(cancellationToken);
            var settings = await _settings.GetAsync(cancellationToken);

            context.Report("Замена бинарника");
            await _process.RunExclusiveAsync(
                (control, token) => SwapAndStartAsync(control, candidatePath, release.Version, previous, settings.XrayAutostart, context, token),
                cancellationToken);

            if (previous is null)
            {
                _logger.LogInformation("Xray {Version} установлен", release.Version);
            }
            else
            {
                _logger.LogInformation("Xray обновлён {Previous} → {Version}", previous, release.Version);
            }

            context.Report($"Готово: установлен Xray {release.Version}");
        }
        finally
        {
            DeleteDirectory(workDirectory);
        }
    }

    private async Task<bool> SwapAndStartAsync(
        IXrayProcessControl control,
        string candidatePath,
        XrayVersion version,
        XrayVersion? previous,
        bool autostart,
        IJobContext context,
        CancellationToken cancellationToken)
    {
        var shouldStart = control.IsRunning || autostart;
        await control.StopAsync(cancellationToken);

        var backupPath = _binaryPath + ".bak";
        var hadPrevious = File.Exists(_binaryPath);
        ReplaceBinary(candidatePath, backupPath, hadPrevious);
        _versionProvider.Invalidate();

        if (!shouldStart)
        {
            return true;
        }

        context.Report("Запуск Xray");
        try
        {
            await control.StartAsync(cancellationToken);
            return true;
        }
        catch (XrayOperationException ex) when (hadPrevious && File.Exists(backupPath))
        {
            context.Report("Новая версия не запустилась, возврат прежней");
            File.Move(backupPath, _binaryPath, overwrite: true);
            _versionProvider.Invalidate();

            try
            {
                await control.StartAsync(cancellationToken);
            }
            catch (XrayOperationException restoreError)
            {
                _logger.LogError(restoreError, "Прежняя версия Xray {Previous} тоже не запустилась", previous);
            }

            throw new XrayOperationException(
                $"Xray {version} не запустился: {ex.Message}. Выполнен откат на версию {previous?.ToString() ?? "прежнюю"}",
                ex);
        }
    }

    private void ReplaceBinary(string candidatePath, string backupPath, bool hadPrevious)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_binaryPath)!);

        var newPath = _binaryPath + ".new";
        File.Copy(candidatePath, newPath, overwrite: true);
        MakeExecutable(newPath);

        if (hadPrevious)
        {
            File.Move(_binaryPath, backupPath, overwrite: true);
        }

        File.Move(newPath, _binaryPath, overwrite: true);
    }

    private static async Task VerifyDigestAsync(string archivePath, string digestPath, CancellationToken cancellationToken)
    {
        XrayDigest digest;
        try
        {
            digest = XrayDigest.Parse(await File.ReadAllTextAsync(digestPath, cancellationToken));
        }
        catch (FormatException ex)
        {
            throw new XrayOperationException($"Некорректный файл .dgst: {ex.Message}", ex);
        }

        await using var archive = File.OpenRead(archivePath);
        var hash = await SHA256.HashDataAsync(archive, cancellationToken);
        if (!digest.Matches(hash))
        {
            throw new XrayOperationException("Контрольная сумма архива не совпала с .dgst: архив повреждён или подменён");
        }
    }

    private static string ExtractBinary(string archivePath, string workDirectory)
    {
        var candidatePath = Path.Combine(workDirectory, BinaryEntryName);
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.Entries.FirstOrDefault(item => item.FullName == BinaryEntryName)
                ?? throw new XrayOperationException($"В архиве нет файла {BinaryEntryName}");
            entry.ExtractToFile(candidatePath, overwrite: true);
        }
        catch (InvalidDataException ex)
        {
            throw new XrayOperationException($"Архив Xray повреждён: {ex.Message}", ex);
        }

        MakeExecutable(candidatePath);
        return candidatePath;
    }

    private async Task VerifyVersionAsync(string candidatePath, XrayVersion expected, CancellationToken cancellationToken)
    {
        ProcessResult result;
        try
        {
            result = await _processRunner.RunAsync(candidatePath, ["version"], VersionTimeout, cancellationToken);
        }
        catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
        {
            throw new XrayOperationException($"Скачанный Xray не запускается: {ex.Message}", ex);
        }

        if (!result.Succeeded || !XrayVersion.TryParse(result.StandardOutput, out var actual))
        {
            throw new XrayOperationException($"Скачанный Xray не сообщил версию: {result.CombinedOutput}");
        }

        if (actual != expected)
        {
            throw new XrayOperationException($"В архиве Xray {actual} вместо ожидаемой {expected}");
        }
    }

    private void EnsureFreeSpace(string path)
    {
        var requiredBytes = _options.MinFreeSpaceMegabytes * 1024L * 1024L;
        if (requiredBytes == 0)
        {
            return;
        }

        var existing = Path.GetFullPath(path);
        while (!Directory.Exists(existing) && Path.GetDirectoryName(existing) is { } parent)
        {
            existing = parent;
        }

        long available;
        try
        {
            available = new DriveInfo(existing).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Не удалось узнать свободное место в {Path}, проверка пропущена", existing);
            return;
        }

        if (available < requiredBytes)
        {
            throw new XrayOperationException(
                $"Недостаточно места в {existing}: свободно {available / 1024 / 1024} МиБ, нужно {_options.MinFreeSpaceMegabytes} МиБ");
        }
    }

    private static void MakeExecutable(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }
    }

    private void DeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Не удалось удалить рабочий каталог {Path}", path);
        }
    }
}
