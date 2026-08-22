using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.PackageManagers;

public class VcpkgStep : IUpdateStep
{
    public string Id => "vcpkg";
    public string Name => "vcpkg C++ Library Manager";
    public string Description => "Upgrades installed vcpkg ports and libraries";
    public StepCategory Category => StepCategory.PackageManagers;
    public int Order => 26;

    private static string? GetVcpkgPath()
    {
        if (ProcessRunner.CommandExists("vcpkg")) return "vcpkg";
        var envVcpkgRoot = Environment.GetEnvironmentVariable("VCPKG_ROOT");
        if (!string.IsNullOrEmpty(envVcpkgRoot))
        {
            var exe = Path.Combine(envVcpkgRoot, "vcpkg.exe");
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(GetVcpkgPath() != null);
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Would execute: vcpkg upgrade --no-dry-run=false");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var exe = GetVcpkgPath() ?? "vcpkg";
        logCallback("Upgrading vcpkg installed packages...");
        var result = await ProcessRunner.RunAsync(exe, "upgrade --no-dry-run", logCallback: logCallback, timeout: TimeSpan.FromMinutes(config.StepTimeoutMinutes), cancellationToken: cancellationToken);

        if (result.Success || result.ExitCode == 0)
        {
            return StepResult.Success(Id, Name, Category, "vcpkg packages updated successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"vcpkg finished with exit code {result.ExitCode}");
    }
}
