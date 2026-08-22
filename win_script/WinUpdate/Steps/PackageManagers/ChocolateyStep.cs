using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.PackageManagers;

public class ChocolateyStep : IUpdateStep
{
    public string Id => "choco";
    public string Name => "Chocolatey";
    public string Description => "Upgrades all installed Chocolatey packages";
    public StepCategory Category => StepCategory.PackageManagers;
    public int Order => 22;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("choco"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync("choco", "outdated", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
        return result.StandardOutput;
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Upgrading Chocolatey packages...");
        var result = await ProcessRunner.RunAsync("choco", "upgrade all -y --no-progress", logCallback: logCallback, timeout: TimeSpan.FromMinutes(config.StepTimeoutMinutes), cancellationToken: cancellationToken);

        if (result.Success || result.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "Chocolatey packages updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Chocolatey completed with exit code {result.ExitCode}");
    }
}
