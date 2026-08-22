using System.Diagnostics;
using System.Text;

namespace WinUpdate.Engine;

public class ProcessExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public bool Success => ExitCode == 0 && !TimedOut;
}

public static class ProcessRunner
{
    public static bool CommandExists(string commandName)
    {
        if (File.Exists(commandName)) return true;

        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var pathExtVariable = Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM";
        var extensions = pathExtVariable.Split(';', StringSplitOptions.RemoveEmptyEntries);

        var paths = pathVariable.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in paths)
        {
            try
            {
                var trimmed = dir.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(trimmed) || !Directory.Exists(trimmed)) continue;

                var directPath = Path.Combine(trimmed, commandName);
                if (File.Exists(directPath)) return true;

                foreach (var ext in extensions)
                {
                    var fullPath = Path.Combine(trimmed, commandName + (ext.StartsWith('.') ? ext : "." + ext));
                    if (File.Exists(fullPath)) return true;
                }
            }
            catch
            {
                // Ignore invalid PATH entries
            }
        }

        return false;
    }

    public static async Task<ProcessExecutionResult> RunAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        Action<string>? logCallback = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuilder.AppendLine(e.Data);
                logCallback?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuilder.AppendLine(e.Data);
                logCallback?.Invoke(e.Data);
            }
        };

        try
        {
            if (!process.Start())
            {
                return new ProcessExecutionResult
                {
                    ExitCode = -1,
                    StandardError = "Failed to start process."
                };
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (timeout.HasValue)
            {
                linkedCts.CancelAfter(timeout.Value);
            }

            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
                return new ProcessExecutionResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = stdoutBuilder.ToString(),
                    StandardError = stderrBuilder.ToString(),
                    TimedOut = false
                };
            }
            catch (OperationCanceledException) when (timeout.HasValue && !cancellationToken.IsCancellationRequested)
            {
                TryKillProcess(process);
                return new ProcessExecutionResult
                {
                    ExitCode = -1,
                    StandardOutput = stdoutBuilder.ToString(),
                    StandardError = "Process timed out.",
                    TimedOut = true
                };
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new ProcessExecutionResult
            {
                ExitCode = -1,
                StandardError = ex.Message
            };
        }
    }

    public static async Task<ProcessExecutionResult> RunPowerShellScriptAsync(
        string script,
        Action<string>? logCallback = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var psExecutable = CommandExists("pwsh") ? "pwsh" : "powershell";
        var bytes = Encoding.Unicode.GetBytes(script);
        var base64 = Convert.ToBase64String(bytes);
        var arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64}";
        return await RunAsync(psExecutable, arguments, null, logCallback, timeout, cancellationToken);
    }

    private static void TryKillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore errors when killing
        }
    }
}
