using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class NodeJsStep : IUpdateStep
{
    public string Id => "node";
    public string Name => "JavaScript / Node.js Ecosystem";
    public string Description => "Updates global packages across npm, yarn, pnpm, bun, and deno";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 36;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        var hasAny = ProcessRunner.CommandExists("npm") ||
                     ProcessRunner.CommandExists("yarn") ||
                     ProcessRunner.CommandExists("pnpm") ||
                     ProcessRunner.CommandExists("bun") ||
                     ProcessRunner.CommandExists("deno");
        return Task.FromResult(config.Node.Enabled && hasAny);
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var tools = new List<string>();
        if (ProcessRunner.CommandExists("npm")) tools.Add("npm -g update");
        if (ProcessRunner.CommandExists("yarn")) tools.Add("yarn global upgrade");
        if (ProcessRunner.CommandExists("pnpm")) tools.Add("pnpm -g update");
        if (ProcessRunner.CommandExists("bun")) tools.Add("bun upgrade");
        if (ProcessRunner.CommandExists("deno")) tools.Add("deno upgrade");
        return Task.FromResult("Would execute commands:\n" + string.Join("\n", tools.Select(t => " - " + t)));
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var executed = new List<string>();

        if (config.Node.UpdateNpm && ProcessRunner.CommandExists("npm"))
        {
            logCallback("Upgrading npm global packages...");
            await ProcessRunner.RunAsync("npm", "update -g", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            executed.Add("npm");
        }

        if (config.Node.UpdateYarn && ProcessRunner.CommandExists("yarn"))
        {
            logCallback("Upgrading yarn global packages...");
            await ProcessRunner.RunAsync("yarn", "global upgrade", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            executed.Add("yarn");
        }

        if (config.Node.UpdatePnpm && ProcessRunner.CommandExists("pnpm"))
        {
            logCallback("Upgrading pnpm global packages...");
            await ProcessRunner.RunAsync("pnpm", "update -g", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
            executed.Add("pnpm");
        }

        if (config.Node.UpdateBun && ProcessRunner.CommandExists("bun"))
        {
            logCallback("Upgrading Bun runtime...");
            await ProcessRunner.RunAsync("bun", "upgrade", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
            executed.Add("bun");
        }

        if (config.Node.UpdateDeno && ProcessRunner.CommandExists("deno"))
        {
            logCallback("Upgrading Deno runtime...");
            await ProcessRunner.RunAsync("deno", "upgrade", logCallback: logCallback, timeout: TimeSpan.FromMinutes(2), cancellationToken: cancellationToken);
            executed.Add("deno");
        }

        return StepResult.Success(Id, Name, Category, $"Updated JS/Node runtimes: {string.Join(", ", executed)}");
    }
}
