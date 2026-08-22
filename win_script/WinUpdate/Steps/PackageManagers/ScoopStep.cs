using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.PackageManagers;

public class ScoopStep : IUpdateStep
{
    public string Id => "scoop";
    public string Name => "Scoop";
    public string Description => "Updates Scoop buckets and installed apps";
    public StepCategory Category => StepCategory.PackageManagers;
    public int Order => 24;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("scoop"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var result = await ProcessRunner.RunAsync("scoop", "status", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
        return result.StandardOutput;
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Updating Scoop buckets...");
        await ProcessRunner.RunAsync("scoop", "update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);

        logCallback("Updating all Scoop applications...");
        var result = await ProcessRunner.RunAsync("scoop", "update *", logCallback: logCallback, timeout: TimeSpan.FromMinutes(config.StepTimeoutMinutes), cancellationToken: cancellationToken);

        if (result.Success || result.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "Scoop apps updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Scoop update finished with code {result.ExitCode}");
    }
}
