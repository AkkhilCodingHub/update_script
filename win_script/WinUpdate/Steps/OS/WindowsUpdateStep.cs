using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.OS;

public class WindowsUpdateStep : IUpdateStep
{
    public string Id => "windows-update";
    public string Name => "Windows Update";
    public string Description => "Checks and installs official Windows OS & driver updates";
    public StepCategory Category => StepCategory.System;
    public int Order => 10;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(OperatingSystem.IsWindows());
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Querying Windows Update Agent COM interface for pending updates...");
        var script = @"
            $session = New-Object -ComObject Microsoft.Update.Session
            $searcher = $session.CreateUpdateSearcher()
            $searchResult = $searcher.Search(""IsInstalled=0 and Type='Software'"")
            if ($searchResult.Updates.Count -eq 0) {
                Write-Output 'No pending Windows Updates found.'
            } else {
                Write-Output ""Found $($searchResult.Updates.Count) pending update(s):""
                foreach ($update in $searchResult.Updates) {
                    Write-Output "" - $($update.Title) (Severity: $($update.MsrcSeverity))""
                }
            }
        ";
        var result = await ProcessRunner.RunPowerShellScriptAsync(script, logCallback, TimeSpan.FromMinutes(3), cancellationToken);
        return result.StandardOutput;
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Initializing Windows Update scan & install session...");

        var script = @"
            $session = New-Object -ComObject Microsoft.Update.Session
            $searcher = $session.CreateUpdateSearcher()
            Write-Output 'Searching for applicable updates...'
            $searchResult = $searcher.Search(""IsInstalled=0 and Type='Software' and IsHidden=0"")
            
            if ($searchResult.Updates.Count -eq 0) {
                Write-Output 'Your system is fully up to date. No pending updates.'
                exit 0
            }

            Write-Output ""Found $($searchResult.Updates.Count) updates to install.""
            $updatesToDownload = New-Object -ComObject Microsoft.Update.UpdateColl
            foreach ($update in $searchResult.Updates) {
                Write-Output "" Queued: $($update.Title)""
                $updatesToDownload.Add($update) | Out-Null
            }

            Write-Output 'Downloading updates...'
            $downloader = $session.CreateUpdateDownloader()
            $downloader.Updates = $updatesToDownload
            $downloadResult = $downloader.Download()
            Write-Output ""Download completed with ResultCode: $($downloadResult.ResultCode)""

            Write-Output 'Installing updates...'
            $installer = $session.CreateUpdateInstaller()
            $installer.Updates = $updatesToDownload
            $installationResult = $installer.Install()
            Write-Output ""Installation completed with ResultCode: $($installationResult.ResultCode). RebootRequired: $($installationResult.RebootRequired)""
        ";

        var timeout = TimeSpan.FromMinutes(config.StepTimeoutMinutes);
        var result = await ProcessRunner.RunPowerShellScriptAsync(script, logCallback, timeout, cancellationToken);

        if (result.Success)
        {
            return StepResult.Success(Id, Name, Category, "Windows updates checked and processed successfully.");
        }

        return StepResult.Warning(Id, Name, Category, $"Windows update completed with notes: {result.StandardError}");
    }
}
