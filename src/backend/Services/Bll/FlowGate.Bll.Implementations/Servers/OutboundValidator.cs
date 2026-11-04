using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;

using Microsoft.Extensions.Options;

using FlowGate.Bll.Implementations.Servers.Parsing;
using FlowGate.Bll.Interfaces.Servers;
using FlowGate.Bll.Interfaces.Xray;
using FlowGate.Contracts.Servers;
using FlowGate.Shared.Bll.Processes;
using FlowGate.Shared.Options;

namespace FlowGate.Bll.Implementations.Servers;

/// <summary>
///     Проверка outbound: структура, затем временный конфиг с этим outbound и <c>xray run -test</c> установленной версией
/// </summary>
public sealed class OutboundValidator : IOutboundValidator
{
    private const string ValidationTag = "validate";
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    private readonly IXrayVersionProvider _versionProvider;
    private readonly IProcessRunner _processRunner;
    private readonly string _binaryPath;
    private readonly string _workDirectory;

    /// <summary>
    ///     Инициализирует новый экземпляр класса <see cref="OutboundValidator"/>
    /// </summary>
    /// <param name="versionProvider">Установлен ли Xray</param>
    /// <param name="processRunner">Запуск <c>xray run -test</c></param>
    /// <param name="options">Настройки Xray</param>
    /// <exception cref="ArgumentNullException">Если любой аргумент равен null</exception>
    public OutboundValidator(IXrayVersionProvider versionProvider, IProcessRunner processRunner, IOptions<XrayOptions> options)
    {
        _versionProvider = versionProvider ?? throw new ArgumentNullException(nameof(versionProvider));
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        ArgumentNullException.ThrowIfNull(options);
        _binaryPath = Path.GetFullPath(options.Value.BinaryPath);
        _workDirectory = Path.GetFullPath(options.Value.WorkDirectory);
    }

    /// <inheritdoc />
    public async Task<OutboundValidationResult> ValidateAsync(JsonObject outbound, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outbound);

        var structural = ValidateStructure(outbound);
        if (structural.Count > 0 || !_versionProvider.IsInstalled)
        {
            return new OutboundValidationResult(structural, CheckedByXray: false);
        }

        Directory.CreateDirectory(_workDirectory);
        var configPath = Path.Combine(_workDirectory, $"validate-{Guid.NewGuid():N}.json");
        try
        {
            var candidate = (JsonObject)outbound.DeepClone();
            candidate["tag"] = ValidationTag;
            var config = new JsonObject
            {
                ["log"] = new JsonObject { ["loglevel"] = "none" },
                ["outbounds"] = new JsonArray(candidate),
            };
            await File.WriteAllTextAsync(configPath, config.ToJsonString(), cancellationToken);

            ProcessResult result;
            try
            {
                result = await _processRunner.RunAsync(_binaryPath, ["run", "-test", "-c", configPath], TestTimeout, cancellationToken);
            }
            catch (Exception ex) when (ex is TimeoutException or Win32Exception or IOException)
            {
                return new OutboundValidationResult([$"Не удалось запустить проверку Xray: {ex.Message}"], CheckedByXray: false);
            }

            return new OutboundValidationResult(
                result.Succeeded ? [] : ExtractErrors(result.CombinedOutput, configPath),
                CheckedByXray: true);
        }
        finally
        {
            File.Delete(configPath);
        }
    }

    /// <summary>
    ///     Достаёт ошибки из вывода <c>xray run -test</c>: убирает баннер версии, отметки времени и путь временного файла
    /// </summary>
    /// <param name="output">Вывод xray</param>
    /// <param name="configPath">Путь временного конфига</param>
    /// <returns>Строки ошибок</returns>
    public static IReadOnlyList<string> ExtractErrors(string output, string configPath)
    {
        ArgumentNullException.ThrowIfNull(output);

        var lines = output
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0
                && !line.StartsWith("Xray ", StringComparison.Ordinal)
                && !line.StartsWith("A unified platform", StringComparison.Ordinal))
            .Select(line => StripTimestamp(line).Replace(configPath, "outbound", StringComparison.Ordinal))
            .ToList();

        var errors = lines
            .Where(line => line.Contains("failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("error", StringComparison.OrdinalIgnoreCase)
                || line.Contains("invalid", StringComparison.OrdinalIgnoreCase)
                || line.Contains("unknown", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var result = errors.Count > 0 ? errors : lines;
        return result.Count > 0 ? result.Select(Shorten).ToList() : ["Xray отклонил outbound без пояснений"];
    }

    /// <summary>
    ///     Сокращает цепочку <c>main: … &gt; infra/conf: … &gt; причина</c> до двух последних звеньев без служебных префиксов
    /// </summary>
    /// <param name="error">Строка ошибки xray</param>
    /// <returns>Короткая строка, например <c>Failed to build REALITY config. → empty "password"</c></returns>
    public static string Shorten(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var parts = error
            .Split(" > ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => part.Replace("infra/conf: ", string.Empty, StringComparison.Ordinal).Replace("Config: ", string.Empty, StringComparison.Ordinal))
            .ToList();

        return parts.Count <= 2 ? string.Join(" → ", parts) : string.Join(" → ", parts.TakeLast(2));
    }

    private static List<string> ValidateStructure(JsonObject outbound)
    {
        var errors = new List<string>();

        var protocol = outbound["protocol"] is JsonValue value && value.GetValueKind() == JsonValueKind.String
            ? value.GetValue<string>()
            : null;

        if (string.IsNullOrWhiteSpace(protocol))
        {
            errors.Add("/protocol: поле обязательно");
        }
        else if (!XrayJsonImporter.SupportedProtocols.Contains(protocol))
        {
            errors.Add($"/protocol: «{protocol}» не поддерживается; допустимы {string.Join(", ", XrayJsonImporter.SupportedProtocols.Order())}");
        }

        if (outbound["settings"] is not JsonObject)
        {
            errors.Add("/settings: нужен объект");
        }

        if (outbound["streamSettings"] is not (null or JsonObject))
        {
            errors.Add("/streamSettings: нужен объект");
        }

        if (XrayJsonImporter.IsChained(outbound))
        {
            errors.Add("/proxySettings, /streamSettings/sockopt/dialerProxy: цепочки прокси не поддерживаются");
        }

        return errors;
    }

    private static string StripTimestamp(string line)
    {
        var bracket = line.IndexOf("] ", StringComparison.Ordinal);
        return line.Length > 20 && char.IsDigit(line[0]) && line[4] == '/' && bracket > 0 ? line[(bracket + 2)..] : line;
    }
}
