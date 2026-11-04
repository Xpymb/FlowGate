using System.Diagnostics;

namespace FlowGate.Shared.Bll.Processes;

/// <summary>
///     Запуск долгоживущих процессов через <see cref="Process"/>
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    /// <inheritdoc />
    public IManagedProcess Start(ProcessStartRequest request, Action<string> onOutputLine)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(onOutputLine);

        var startInfo = new ProcessStartInfo(request.FileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = request.WorkingDirectory ?? string.Empty,
        };

        foreach (var argument in request.Arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => ForwardLine(e.Data, onOutputLine);
        process.ErrorDataReceived += (_, e) => ForwardLine(e.Data, onOutputLine);

        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException($"Не удалось запустить {request.FileName}");
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return new ManagedProcess(process);
    }

    private static void ForwardLine(string? line, Action<string> onOutputLine)
    {
        if (line is not null)
        {
            onOutputLine(line);
        }
    }
}
