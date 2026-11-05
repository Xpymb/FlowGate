using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Процессы из <c>/proc/&lt;pid&gt;/exe</c> (Linux); на других ОС список пуст
/// </summary>
public sealed class ProcFsProcessTable : IProcessTable
{
    /// <inheritdoc />
    public IReadOnlyList<int> FindByExecutable(string executablePrefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePrefix);

        if (!OperatingSystem.IsLinux() || !Directory.Exists("/proc"))
        {
            return [];
        }

        var self = Environment.ProcessId;
        return Directory.EnumerateDirectories("/proc")
            .Select(directory => int.TryParse(Path.GetFileName(directory), NumberStyles.None, CultureInfo.InvariantCulture, out var pid) ? pid : 0)
            .Where(pid => pid > 0 && pid != self && Executable(pid)?.StartsWith(executablePrefix, StringComparison.Ordinal) == true)
            .ToList();
    }

    /// <inheritdoc />
    public void Kill(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            process.Kill();
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
        {
        }
    }

    private static string? Executable(int pid)
    {
        try
        {
            return File.ResolveLinkTarget($"/proc/{pid}/exe", returnFinalTarget: false)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
