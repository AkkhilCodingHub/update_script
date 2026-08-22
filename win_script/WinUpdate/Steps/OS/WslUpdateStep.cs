using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.OS;

public class WslUpdateStep : IUpdateStep
{
    public string Id => "wsl";
    public string Name => "Windows Subsystem for Linux (WSL)";
    public string Description => "Updates WSL kernel and package components";
    public StepCategory Category => StepCategory.System;
    public int Order => 15;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("wsl.exe") || ProcessRunner.CommandExists("wsl"));
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Would execute: wsl.exe --update");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Checking for WSL kernel and component updates...");
        var result = await ProcessRunner.RunAsync("wsl.exe", "--update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        if (result.Success || result.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "WSL update check completed.");
        }

        return StepResult.Warning(Id, Name, Category, $"WSL update completed with exit code {result.ExitCode}");
    }
}
