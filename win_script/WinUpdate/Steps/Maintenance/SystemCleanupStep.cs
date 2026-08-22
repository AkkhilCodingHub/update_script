using System.Runtime.InteropServices;
using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.Maintenance;

public class SystemCleanupStep : IUpdateStep
{
    public string Id => "cleanup";
    public string Name => "System Cleanup & Cache Flush";
    public string Description => "Cleans temporary directories, cache stores, and frees system disk space";
    public StepCategory Category => StepCategory.Maintenance;
    public int Order => 90;

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    private const uint SHERB_NOCONFIRMATION = 0x00000001;
    private const uint SHERB_NOPROGRESSUI = 0x00000002;
    private const uint SHERB_NOSOUND = 0x00000004;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(config.Cleanup.Enabled && OperatingSystem.IsWindows());
    }

    public Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        var targets = new List<string>();
        if (config.Cleanup.ClearUserTemp) targets.Add(Path.GetTempPath());
        if (config.Cleanup.ClearSystemTemp) targets.Add(@"C:\Windows\Temp");
        if (config.Cleanup.ClearDeliveryOptimization) targets.Add(@"C:\Windows\SoftwareDistribution\DeliveryOptimization");
        if (config.Cleanup.EmptyRecycleBin) targets.Add("Windows Recycle Bin");

        return Task.FromResult("Would clean temporary locations:\n" + string.Join("\n", targets.Select(t => " - " + t)));
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        long totalBytesFreed = 0;
        int filesDeleted = 0;

        await Task.Run(() =>
        {
            // 1. User Temp
            if (config.Cleanup.ClearUserTemp)
            {
                var userTemp = Path.GetTempPath();
                logCallback($"Cleaning User Temp: {userTemp}...");
                CleanDirectory(userTemp, ref totalBytesFreed, ref filesDeleted, logCallback);
            }

            // 2. System Temp
            if (config.Cleanup.ClearSystemTemp)
            {
                var sysTemp = @"C:\Windows\Temp";
                if (Directory.Exists(sysTemp))
                {
                    logCallback($"Cleaning System Temp: {sysTemp}...");
                    CleanDirectory(sysTemp, ref totalBytesFreed, ref filesDeleted, logCallback);
                }
            }

            // 3. Delivery Optimization Cache
            if (config.Cleanup.ClearDeliveryOptimization)
            {
                var doDir = @"C:\Windows\SoftwareDistribution\DeliveryOptimization";
                if (Directory.Exists(doDir))
                {
                    logCallback($"Cleaning Delivery Optimization Cache...");
                    CleanDirectory(doDir, ref totalBytesFreed, ref filesDeleted, logCallback);
                }
            }

            // 4. Recycle Bin
            if (config.Cleanup.EmptyRecycleBin)
            {
                try
                {
                    logCallback("Emptying Windows Recycle Bin...");
                    SHEmptyRecycleBin(IntPtr.Zero, null, SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI | SHERB_NOSOUND);
                    logCallback("✔ Recycle Bin emptied.");
                }
                catch (Exception ex)
                {
                    logCallback($"⚠ Could not empty Recycle Bin: {ex.Message}");
                }
            }
        }, cancellationToken);

        var mbFreed = (double)totalBytesFreed / (1024 * 1024);
        var summary = $"Cleaned {filesDeleted} temporary files, freeing {mbFreed:F2} MB of disk space.";
        logCallback($"✔ {summary}");
        return StepResult.Success(Id, Name, Category, summary);
    }

    private static void CleanDirectory(string path, ref long totalBytesFreed, ref int filesDeleted, Action<string> logCallback)
    {
        if (!Directory.Exists(path)) return;

        try
        {
            foreach (var file in Directory.GetFiles(path))
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    // Only delete files older than 4 hours to avoid active writes
                    if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromHours(4))
                    {
                        var size = fileInfo.Length;
                        fileInfo.Attributes = FileAttributes.Normal;
                        fileInfo.Delete();
                        Interlocked.Add(ref totalBytesFreed, size);
                        Interlocked.Increment(ref filesDeleted);
                    }
                }
                catch
                {
                    // Locked file in use by another process - ignore safely
                }
            }

            foreach (var dir in Directory.GetDirectories(path))
            {
                try
                {
                    CleanDirectory(dir, ref totalBytesFreed, ref filesDeleted, logCallback);
                    if (!Directory.EnumerateFileSystemEntries(dir).Any())
                    {
                        Directory.Delete(dir);
                    }
                }
                catch
                {
                    // Directory in use or permission issue
                }
            }
        }
        catch
        {
            // Directory read issue
        }
    }
}
