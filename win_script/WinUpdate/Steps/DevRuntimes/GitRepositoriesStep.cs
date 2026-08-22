using System.Collections.Concurrent;
using System.Text;
using WinUpdate.Configuration;
using WinUpdate.Engine;

namespace WinUpdate.Steps.DevRuntimes;

public class GitRepositoriesStep : IUpdateStep
{
    public string Id => "git";
    public string Name => "Git Repositories";
    public string Description => "Discovers and updates all local Git repositories across user folders and drives";
    public StepCategory Category => StepCategory.DevRuntimes;
    public int Order => 30;

    public Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(config.Git.Enabled && ProcessRunner.CommandExists("git"));
    }

    public async Task<string> GetDryRunPlanAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Scanning for Git repositories...");
        var repos = await DiscoverRepositoriesAsync(config, logCallback, cancellationToken);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Found {repos.Count} Git repositories:");
        foreach (var repo in repos)
        {
            sb.AppendLine($" - {repo}");
        }
        return sb.ToString();
    }

    public async Task<StepResult> ExecuteAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken = default)
    {
        logCallback("Scanning system drives and user folders for Git repositories...");
        var repos = await DiscoverRepositoriesAsync(config, logCallback, cancellationToken);

        if (repos.Count == 0)
        {
            return StepResult.Success(Id, Name, Category, "No Git repositories found to update.");
        }

        logCallback($"Discovered {repos.Count} repository paths. Starting parallel updates (Concurrency: {config.MaxGitParallelism})...");

        var updatedCount = 0;
        var failedCount = 0;
        var skippedCount = 0;
        var failures = new ConcurrentBag<string>();

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.ParallelGit ? Math.Max(1, config.MaxGitParallelism) : 1,
            CancellationToken = cancellationToken
        };

        await Parallel.ForEachAsync(repos, options, async (repoPath, ct) =>
        {
            try
            {
                var repoName = Path.GetFileName(repoPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                
                // First check if repository has dirty changes
                var statusRes = await ProcessRunner.RunAsync("git", "status --porcelain", repoPath, null, TimeSpan.FromSeconds(20), ct);
                var isDirty = !string.IsNullOrWhiteSpace(statusRes.StandardOutput);

                // Fetch remote
                var fetchRes = await ProcessRunner.RunAsync("git", "fetch --prune --quiet", repoPath, null, TimeSpan.FromMinutes(2), ct);
                if (!fetchRes.Success)
                {
                    Interlocked.Increment(ref failedCount);
                    failures.Add($"{repoName} ({repoPath}): Fetch failed - {fetchRes.StandardError.Trim()}");
                    logCallback($"❌ [{repoName}] Fetch failed: {fetchRes.StandardError.Trim()}");
                    return;
                }

                if (config.Git.FetchOnly)
                {
                    Interlocked.Increment(ref updatedCount);
                    logCallback($"✔ [{repoName}] Fetched.");
                    return;
                }

                if (isDirty)
                {
                    Interlocked.Increment(ref skippedCount);
                    logCallback($"⚠ [{repoName}] Skipped pull (uncommitted changes detected).");
                    return;
                }

                // Pull with ff-only to prevent unexpected merge commits
                var pullRes = await ProcessRunner.RunAsync("git", "pull --ff-only --quiet", repoPath, null, TimeSpan.FromMinutes(2), ct);
                if (pullRes.Success)
                {
                    Interlocked.Increment(ref updatedCount);
                    logCallback($"✔ [{repoName}] Updated successfully.");
                }
                else
                {
                    Interlocked.Increment(ref skippedCount);
                    logCallback($"⚠ [{repoName}] Pull skipped (cannot fast-forward or no upstream configured).");
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref failedCount);
                failures.Add($"{repoPath}: {ex.Message}");
            }
        });

        var summary = $"Git Repositories: {updatedCount} updated, {skippedCount} skipped/dirty, {failedCount} failed out of {repos.Count} total.";
        if (failedCount > 0)
        {
            return StepResult.Warning(Id, Name, Category, summary);
        }

        return StepResult.Success(Id, Name, Category, summary);
    }

    private static async Task<List<string>> DiscoverRepositoriesAsync(AppConfig config, Action<string> logCallback, CancellationToken cancellationToken)
    {
        var foundRepos = new ConcurrentBag<string>();
        var rootSearchDirectories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Add custom search paths
        foreach (var customPath in config.Git.CustomSearchPaths)
        {
            if (Directory.Exists(customPath))
            {
                rootSearchDirectories.Add(customPath);
            }
        }

        // Add standard user folders
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var standardFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(userProfile, "Downloads"),
            Path.Combine(userProfile, "Source"),
            Path.Combine(userProfile, "Repos"),
            Path.Combine(userProfile, "Projects"),
            Path.Combine(userProfile, "Development"),
            Path.Combine(userProfile, "workspace"),
            Path.Combine(userProfile, "github")
        };

        foreach (var folder in standardFolders)
        {
            if (Directory.Exists(folder))
            {
                rootSearchDirectories.Add(folder);
            }
        }

        // Add drive roots if configured
        if (config.Git.ScanAllDrives)
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (drive.IsReady && (drive.DriveType == DriveType.Fixed || drive.DriveType == DriveType.Removable))
                    {
                        // Look for common project folders on secondary drives, e.g. D:\Projects, D:\src, D:\Repos
                        var driveRoot = drive.RootDirectory.FullName;
                        var candidates = new[] { "Projects", "Source", "Repos", "Development", "Code", "git", "github", "workspace" };
                        foreach (var cand in candidates)
                        {
                            var sub = Path.Combine(driveRoot, cand);
                            if (Directory.Exists(sub)) rootSearchDirectories.Add(sub);
                        }
                    }
                }
                catch
                {
                    // Ignore inaccessible drives
                }
            }
        }

        var ignoredSet = new HashSet<string>(config.Git.IgnoredDirectories, StringComparer.OrdinalIgnoreCase);

        await Task.Run(() =>
        {
            Parallel.ForEach(rootSearchDirectories, root =>
            {
                ScanDirectoryRecursive(root, 0, config.Git.MaxSearchDepth, ignoredSet, foundRepos, cancellationToken);
            });
        }, cancellationToken);

        return foundRepos.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
    }

    private static void ScanDirectoryRecursive(
        string currentDir, 
        int currentDepth, 
        int maxDepth, 
        HashSet<string> ignoredSet, 
        ConcurrentBag<string> results, 
        CancellationToken cancellationToken)
    {
        if (currentDepth > maxDepth || cancellationToken.IsCancellationRequested) return;

        try
        {
            var dirInfo = new DirectoryInfo(currentDir);
            if ((dirInfo.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0 && currentDepth > 0)
            {
                return;
            }

            if (ignoredSet.Contains(dirInfo.Name))
            {
                return;
            }

            // Check if current directory is a git repository
            var gitDir = Path.Combine(currentDir, ".git");
            if (Directory.Exists(gitDir) || File.Exists(gitDir)) // submodule or worktree may have .git file
            {
                results.Add(currentDir);
                // Do not recurse deeper into subfolders of a git repo except if submodules exist
                return;
            }

            var subDirectories = Directory.GetDirectories(currentDir);
            foreach (var subDir in subDirectories)
            {
                var name = Path.GetFileName(subDir);
                if (ignoredSet.Contains(name) || name.StartsWith('.')) continue;

                ScanDirectoryRecursive(subDir, currentDepth + 1, maxDepth, ignoredSet, results, cancellationToken);
            }
        }
        catch
        {
            // Ignore unauthorized access or transient file lock exceptions during search
        }
    }
}
