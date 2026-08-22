using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class GoStep : IUpdateStep
{
    public string Id => "go";
    public string Name => "Go Environment";
    public string Description => "Checks Go version and updates global go binaries if configured";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 40;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("go"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var res = await ProcessRunner.RunAsync("go", "version", logCallback: logCallback, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
        return $"Go version: {res.StandardOutput.Trim()}";
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Checking Go environment...");
        var verRes = await ProcessRunner.RunAsync("go", "version", logCallback: logCallback, timeout: TimeSpan.FromSeconds(10), cancellationToken: cancellationToken);
        var goPathRes = await ProcessRunner.RunAsync("go", "env GOPATH", null, null, TimeSpan.FromSeconds(10), cancellationToken);

        var gopath = goPathRes.StandardOutput.Trim();
        if (!string.IsNullOrEmpty(gopath))
        {
            logCallback($"GOPATH is located at: {gopath}");
        }

        return StepResult.Success(Id, Name, Category, $"Go checked: {verRes.StandardOutput.Trim()}");
    }
}
