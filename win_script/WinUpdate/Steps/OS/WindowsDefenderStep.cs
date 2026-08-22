using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.OS;

public class WindowsDefenderStep : IUpdateStep
{
    public string Id => "windows-defender";
    public string Name => "Windows Defender Signatures";
    public string Description => "Updates antivirus and antimalware security definitions";
    public StepCategory Category => StepCategory.System;
    public int Order => 12;

    private static string? GetMpCmdRunPath()
    {
        var standardPath = @"C:\Program Files\Windows Defender\MpCmdRun.exe";
        if (File.Exists(standardPath)) return standardPath;

        var platformDir = @"C:\ProgramData\Microsoft\Windows Defender\platform";
        if (Directory.Exists(platformDir))
        {
            var exe = Directory.GetFiles(platformDir, "MpCmdRun.exe", SearchOption.AllDirectories)
                .OrderByDescending(f => f)
                .FirstOrDefault();
            if (exe != null) return exe;
        }

        return null;
    }

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetMpCmdRunPath() != null);
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var exePath = GetMpCmdRunPath();
        return Task.FromResult($"Would execute: \"{exePath}\" -SignatureUpdate");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var exePath = GetMpCmdRunPath();
        if (exePath == null)
        {
            return StepResult.Skipped(Id, Name, Category, "MpCmdRun.exe not found.");
        }

        logCallback("Updating Microsoft Defender virus & spyware signatures...");
        var result = await ProcessRunner.RunAsync(
            exePath, 
            "-SignatureUpdate", 
            logCallback: logCallback, 
            timeout: TimeSpan.FromMinutes(3), 
            cancellationToken: cancellationToken);

        if (result.Success || result.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "Defender signatures updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Signature update finished with code {result.ExitCode}");
    }
}
