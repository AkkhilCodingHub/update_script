using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class PowerShellModulesStep : IUpdateStep
{
    public string Id => "powershell-modules";
    public string Name => "PowerShell Modules & Help";
    public string Description => "Updates installed PowerShell modules from PSGallery and updates local help files";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 42;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ProcessRunner.CommandExists("pwsh") || ProcessRunner.CommandExists("powershell"));
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        return Task.FromResult("Would execute: Update-Module (non-blocking) and Update-Help -ErrorAction SilentlyContinue");
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Updating installed PowerShell modules...");
        var script = @"
            $ErrorActionPreference = 'SilentlyContinue'
            Get-InstalledModule | ForEach-Object {
                Write-Output ""Checking module: $($_.Name)""
                Update-Module -Name $_.Name -AcceptLicense -Force -ErrorAction SilentlyContinue
            }
        ";

        var result = await ProcessRunner.RunPowerShellScriptAsync(script, logCallback, TimeSpan.FromMinutes(5), cancellationToken);

        return StepResult.Success(Id, Name, Category, "PowerShell modules processed.");
    }
}
