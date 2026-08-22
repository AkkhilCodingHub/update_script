using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.PackageManagers;

public class WingetStep : IUpdateStep
{
    public string Id => "winget";
    public string Name => "Windows Package Manager (Winget)";
    public string Description => "Upgrades all installed Windows desktop applications and packages";
    public StepCategory Category => StepCategory.PackageManagers;
    public int Order => 20;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(config.Winget.Enabled && ProcessRunner.CommandExists("winget"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Querying winget for available upgrades...");
        var result = await ProcessRunner.RunAsync("winget", "upgrade", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
        return result.StandardOutput;
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Starting winget upgrade for all packages...");

        var args = "upgrade --all --accept-package-agreements --accept-source-agreements";
        if (config.Winget.IncludeUnknown)
        {
            args += " --include-unknown";
        }

        var timeout = TimeSpan.FromMinutes(config.StepTimeoutMinutes);
        var result = await ProcessRunner.RunAsync("winget", args, logCallback: logCallback, timeout: timeout, cancellationToken: cancellationToken);

        // Winget exit code 0 or -1978335189 (no updates found / 0x8A15002B) are successful
        if (result.ExitCode == 0 || result.ExitCode == -1978335189 || result.StandardOutput.Contains("No applicable update found"))
        {
            return StepResult.Success(Id, Name, Category, "Winget packages updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Winget completed with exit code {result.ExitCode}");
    }
}
