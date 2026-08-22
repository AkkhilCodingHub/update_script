using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class DotnetToolsStep : IUpdateStep
{
    public string Id => "dotnet";
    public string Name => ".NET Global Tools & Workloads";
    public string Description => "Updates installed .NET global CLI tools and SDK workloads";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 32;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(config.Dotnet.Enabled && ProcessRunner.CommandExists("dotnet"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var listRes = await ProcessRunner.RunAsync("dotnet", "tool list -g", logCallback: logCallback, timeout: TimeSpan.FromMinutes(1), cancellationToken: cancellationToken);
        return "Installed global tools:\n" + listRes.StandardOutput;
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Listing installed .NET global tools...");
        var listRes = await ProcessRunner.RunAsync("dotnet", "tool list -g", null, null, TimeSpan.FromMinutes(1), cancellationToken);

        var toolNames = new List<string>();
        if (listRes.Success)
        {
            var lines = listRes.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            // Skip the header lines (Package Id, Version, Commands)
            var contentLines = lines.Skip(2);
            foreach (var line in contentLines)
            {
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0]))
                {
                    toolNames.Add(parts[0]);
                }
            }
        }

        var updated = 0;
        var failed = 0;

        if (config.Dotnet.UpdateGlobalTools && toolNames.Count > 0)
        {
            logCallback($"Found {toolNames.Count} global .NET tool(s) to update...");
            foreach (var tool in toolNames)
            {
                logCallback($"Updating .NET tool: {tool}...");
                var updateRes = await ProcessRunner.RunAsync("dotnet", $"tool update -g {tool}", logCallback: logCallback, timeout: TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);
                if (updateRes.Success) updated++;
                else failed++;
            }
        }
        else
        {
            logCallback("No global .NET tools found to update.");
        }

        if (config.Dotnet.UpdateWorkloads)
        {
            logCallback("Checking for .NET SDK workload updates...");
            var workloadRes = await ProcessRunner.RunAsync("dotnet", "workload update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            if (!workloadRes.Success && workloadRes.ExitCode != 0)
            {
                logCallback($"⚠ Workload update note: {workloadRes.StandardError}");
            }
        }

        var msg = $"Updated {updated} .NET tool(s), {failed} failed.";
        return failed > 0 ? StepResult.Warning(Id, Name, Category, msg) : StepResult.Success(Id, Name, Category, msg);
    }
}
