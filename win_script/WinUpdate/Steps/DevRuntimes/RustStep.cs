using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class RustStep : IUpdateStep
{
    public string Id => "rust";
    public string Name => "Rust (rustup & cargo)";
    public string Description => "Updates the Rust toolchain and cargo-installed global binaries";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 38;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("rustup") || ProcessRunner.CommandExists("cargo"));
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var actions = new List<string>();
        if (ProcessRunner.CommandExists("rustup")) actions.Add("rustup update");
        if (ProcessRunner.CommandExists("cargo-install-update")) actions.Add("cargo install-update -a");
        if (ProcessRunner.CommandExists("cargo-binstall")) actions.Add("cargo binstall --all");
        return Task.FromResult("Would execute:\n" + string.Join("\n", actions.Select(a => " - " + a)));
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        if (ProcessRunner.CommandExists("rustup"))
        {
            logCallback("Updating Rust toolchains via rustup...");
            await ProcessRunner.RunAsync("rustup", "update", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        }

        if (ProcessRunner.CommandExists("cargo-install-update") || ProcessRunner.CommandExists("cargo-install-update.exe"))
        {
            logCallback("Updating cargo-installed binaries...");
            await ProcessRunner.RunAsync("cargo", "install-update -a", logCallback: logCallback, timeout: TimeSpan.FromMinutes(10), cancellationToken: cancellationToken);
        }
        else if (ProcessRunner.CommandExists("cargo-binstall") || ProcessRunner.CommandExists("cargo-binstall.exe"))
        {
            logCallback("Updating cargo binaries via cargo-binstall...");
            await ProcessRunner.RunAsync("cargo", "binstall --all --no-confirm", logCallback: logCallback, timeout: TimeSpan.FromMinutes(5), cancellationToken: cancellationToken);
        }

        return StepResult.Success(Id, Name, Category, "Rust toolchain and binaries updated.");
    }
}
