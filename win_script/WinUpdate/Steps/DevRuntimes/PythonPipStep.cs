using System.Text;
using System.Text.Json;
using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class PythonPipStep : IUpdateStep
{
    public string Id => "python";
    public string Name => "Python (pip, pipx & uv)";
    public string Description => "Updates outdated global Python packages, pipx applications, and uv tools";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 34;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var hasPip = ProcessRunner.CommandExists("pip") || ProcessRunner.CommandExists("pip3") || ProcessRunner.CommandExists("python");
        var hasPipx = ProcessRunner.CommandExists("pipx");
        var hasUv = ProcessRunner.CommandExists("uv");
        return Task.FromResult(config.Python.Enabled && (hasPip || hasPipx || hasUv));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var sb = new System.Text.StringBuilder();
        if (ProcessRunner.CommandExists("pip"))
        {
            var pipList = await ProcessRunner.RunAsync("pip", "list --outdated", logCallback: logCallback, timeout: TimeSpan.FromMinutes(1), cancellationToken: cancellationToken);
            sb.AppendLine("Outdated pip packages:\n" + pipList.StandardOutput);
        }
        if (ProcessRunner.CommandExists("pipx"))
        {
            sb.AppendLine("Would execute: pipx upgrade-all");
        }
        if (ProcessRunner.CommandExists("uv"))
        {
            sb.AppendLine("Would execute: uv tool upgrade --all");
        }
        return sb.ToString();
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var updated = 0;
        var failed = 0;

        // 1. Upgrade pip itself & outdated packages
        if (config.Python.UpdatePipPackages && ProcessRunner.CommandExists("pip"))
        {
            logCallback("Upgrading pip itself...");
            await ProcessRunner.RunAsync("python", "-m pip install --upgrade pip", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);

            logCallback("Querying outdated pip packages in JSON format...");
            var outdatedRes = await ProcessRunner.RunAsync("pip", "list --outdated --format=json", null, null, TimeSpan.FromMinutes(2), cancellationToken);

            if (outdatedRes.Success && !string.IsNullOrWhiteSpace(outdatedRes.StandardOutput))
            {
                try
                {
                    using var doc = JsonDocument.Parse(outdatedRes.StandardOutput);
                    var packages = doc.RootElement.EnumerateArray().ToList();
                    logCallback($"Found {packages.Count} outdated pip package(s)...");

                    foreach (var pkg in packages)
                    {
                        var name = pkg.GetProperty("name").GetString();
                        if (string.IsNullOrEmpty(name)) continue;

                        logCallback($"Upgrading pip package: {name}...");
                        var upgradeRes = await ProcessRunner.RunAsync("pip", $"install --upgrade {name}", logCallback: logCallback, timeout: TimeSpan.FromMinutes(3), cancellationToken: cancellationToken);
                        if (upgradeRes.Success) updated++;
                        else failed++;
                    }
                }
                catch (Exception ex)
                {
                    logCallback($"⚠ Could not parse outdated pip packages: {ex.Message}");
                }
            }
            else
            {
                logCallback("All pip packages are up to date.");
            }
        }

        // 2. Upgrade pipx tools
        if (config.Python.UpdatePipx && ProcessRunner.CommandExists("pipx"))
        {
            logCallback("Upgrading pipx applications...");
            var pipxRes = await ProcessRunner.RunAsync("pipx", "upgrade-all", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            if (pipxRes.Success) logCallback("✔ pipx packages upgraded.");
        }

        // 3. Upgrade uv tools
        if (config.Python.UpdateUv && ProcessRunner.CommandExists("uv"))
        {
            logCallback("Upgrading uv tools...");
            var uvRes = await ProcessRunner.RunAsync("uv", "tool upgrade --all", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            if (uvRes.Success) logCallback("✔ uv tools upgraded.");
        }

        var summary = $"Python updates completed ({updated} pip packages upgraded, {failed} failed).";
        return failed > 0 ? StepResult.Warning(Id, Name, Category, summary) : StepResult.Success(Id, Name, Category, summary);
    }
}
