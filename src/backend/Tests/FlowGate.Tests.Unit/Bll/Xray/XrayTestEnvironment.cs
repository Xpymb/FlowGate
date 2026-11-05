using Microsoft.Extensions.Logging.Abstractions;

using FlowGate.Bll.Implementations.Xray;
using FlowGate.Dal.Implementations.Settings;
using FlowGate.Dal.Implementations.Xray;
using FlowGate.Shared.Bll.Jobs;
using FlowGate.Shared.Dal.Json;
using FlowGate.Shared.GitHub;
using FlowGate.Shared.Options;
using FlowGate.Tests.Common.Processes;
using FlowGate.Tests.Common.Xray;

namespace FlowGate.Tests.Unit.Bll.Xray;

/// <summary>
///     Настоящие сервисы Xray на временном каталоге. Подменены только внешние границы: GitHub, запуск процессов, время
/// </summary>
public sealed class XrayTestEnvironment : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "flowgate-tests", Guid.NewGuid().ToString("N"));

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="XrayTestEnvironment"/>
    /// </summary>
    /// <param name="timeProvider">Источник времени; по умолчанию системный</param>
    /// <param name="startTimeoutSeconds">Сколько процесс должен проработать, чтобы запуск считался успешным</param>
    public XrayTestEnvironment(TimeProvider? timeProvider = null, int startTimeoutSeconds = 0)
    {
        TimeProvider = timeProvider ?? TimeProvider.System;

        var flowGateOptions = Microsoft.Extensions.Options.Options.Create(new FlowGateOptions
        {
            DataDirectory = Path.Combine(_root, "data"),
        });
        XrayOptions = new XrayOptions
        {
            BinaryPath = Path.Combine(_root, "bin", "xray"),
            WorkDirectory = Path.Combine(_root, "tmp"),
            StartTimeoutSeconds = startTimeoutSeconds,
            MinFreeSpaceMegabytes = 0,
        };
        var xrayOptions = Microsoft.Extensions.Options.Options.Create(XrayOptions);

        ConfigFile = new XrayConfigFileProvider(flowGateOptions, xrayOptions);
        Settings = new SettingsProvider(new JsonFileStoreFactory(flowGateOptions));
        Logs = new XrayLogBuffer(xrayOptions, TimeProvider);
        Process = new XrayProcessService(xrayOptions, ConfigFile, Launcher, Logs, TimeProvider, NullLogger<XrayProcessService>.Instance);
        VersionProvider = new XrayVersionProvider(xrayOptions, Runner, NullLogger<XrayVersionProvider>.Instance);
        ConfigService = new XrayConfigService(
            new EmptyXrayConfigSource(),
            new XrayConfigBuilder(),
            ConfigFile,
            Settings,
            Runner,
            Process,
            xrayOptions,
            NullLogger<XrayConfigService>.Instance);

        var gitHubClient = new GitHubReleasesClient(
            new HttpClient(GitHub.Handler) { BaseAddress = FakeXrayGitHub.ApiBaseAddress },
            Microsoft.Extensions.Options.Options.Create(new GitHubOptions()));
        Releases = new XrayReleaseService(gitHubClient, xrayOptions, TimeProvider);
        Jobs = new JobRunner(TimeProvider.System, NullLogger<JobRunner>.Instance);
        Installer = new XrayInstaller(
            Releases,
            gitHubClient,
            Runner,
            Process,
            VersionProvider,
            ConfigService,
            Settings,
            Jobs,
            xrayOptions,
            NullLogger<XrayInstaller>.Instance);
        Service = new XrayService(
            Process,
            VersionProvider,
            Releases,
            Installer,
            ConfigService,
            Settings,
            Logs,
            xrayOptions,
            NullLogger<XrayService>.Instance);
    }

    /// <summary>
    ///     Источник времени
    /// </summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>
    ///     Настройки Xray
    /// </summary>
    public XrayOptions XrayOptions { get; }

    /// <summary>
    ///     Подменный GitHub
    /// </summary>
    public FakeXrayGitHub GitHub { get; } = new();

    /// <summary>
    ///     Подменный запуск команд
    /// </summary>
    public FakeProcessRunner Runner { get; } = new();

    /// <summary>
    ///     Подменный запуск процессов
    /// </summary>
    public FakeProcessLauncher Launcher { get; } = new();

    /// <summary>
    ///     Файл конфигурации
    /// </summary>
    public XrayConfigFileProvider ConfigFile { get; }

    /// <summary>
    ///     Пользовательские настройки
    /// </summary>
    public SettingsProvider Settings { get; }

    /// <summary>
    ///     Буфер вывода
    /// </summary>
    public XrayLogBuffer Logs { get; }

    /// <summary>
    ///     Супервизор процесса
    /// </summary>
    public XrayProcessService Process { get; }

    /// <summary>
    ///     Установленная версия
    /// </summary>
    public XrayVersionProvider VersionProvider { get; }

    /// <summary>
    ///     Конфигурация Xray
    /// </summary>
    public XrayConfigService ConfigService { get; }

    /// <summary>
    ///     Релизы
    /// </summary>
    public XrayReleaseService Releases { get; }

    /// <summary>
    ///     Фоновые задачи
    /// </summary>
    public JobRunner Jobs { get; }

    /// <summary>
    ///     Установщик
    /// </summary>
    public XrayInstaller Installer { get; }

    /// <summary>
    ///     Фасад для API
    /// </summary>
    public XrayService Service { get; }

    /// <summary>
    ///     Путь к бинарнику
    /// </summary>
    public string BinaryPath => Path.GetFullPath(XrayOptions.BinaryPath);

    /// <summary>
    ///     Кладёт фейковый бинарник указанной версии, как будто Xray уже установлен
    /// </summary>
    /// <param name="version">Версия</param>
    public void InstallFakeBinary(string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BinaryPath)!);
        File.WriteAllText(BinaryPath, FakeXrayBinary.Content(version));
    }

    /// <summary>
    ///     Ждёт выполнения условия (для фоновых продолжений супервизора)
    /// </summary>
    /// <param name="condition">Условие</param>
    /// <returns>Задача, завершающаяся при выполнении условия</returns>
    /// <exception cref="TimeoutException">Если условие не выполнилось за 5 с</exception>
    public static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Условие не выполнилось за 5 с");
            }

            await Task.Delay(10);
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Process.Dispose();
        Jobs.Dispose();

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
