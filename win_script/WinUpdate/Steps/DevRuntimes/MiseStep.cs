using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class MiseStep : IUpdateStep
{
    public string Id => "mise";
    public string Name => "Mise (Dev Tool Version Manager)";
    public string Description => "Updates mise binary, managed dev runtimes/tools, and plugins";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 31;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("mise"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Would execute: mise self-update -y");
        sb.AppendLine("Would execute: mise plugins update");
        var res = await ProcessRunner.RunAsync("mise", "upgrade --dry-run", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
        if (!string.IsNullOrWhiteSpace(res.StandardOutput))
        {
            sb.AppendLine("Mise tools pending upgrade:\n" + res.StandardOutput);
        }
        return sb.ToString();
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Updating mise self binary...");
        await ProcessRunner.RunAsync("mise", "self-update -y", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);

        logCallback("Updating mise plugins...");
        await ProcessRunner.RunAsync("mise", "plugins update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);

        logCallback("Upgrading all mise managed tools and runtimes...");
        var res = await ProcessRunner.RunAsync("mise", "upgrade -y", logCallback: logCallback, timeout: TimeSpan.FromMinutes(config.StepTimeoutMinutes), cancellationToken: cancellationToken);

        if (res.Success || res.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "Mise and managed tools updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Mise finished with exit code {res.ExitCode}");
    }
}
