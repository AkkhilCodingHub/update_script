using System.Text.Json;
using System.Text.Json.Serialization;

namespace WinUpdate.Configuration;

public class AppConfig
{
    public bool DryRun { get; set; } = false;
    public bool Verbose { get; set; } = false;
    public bool ParallelGit { get; set; } = true;
    public int MaxGitParallelism { get; set; } = 8;
    public int StepTimeoutMinutes { get; set; } = 15;
    public List<string> OnlySteps { get; set; } = new();
    public List<string> SkipSteps { get; set; } = new();
    public List<string> OnlyCategories { get; set; } = new();

    public GitConfig Git { get; set; } = new();
    public WingetConfig Winget { get; set; } = new();
    public DotnetConfig Dotnet { get; set; } = new();
    public PythonConfig Python { get; set; } = new();
    public NodeConfig Node { get; set; } = new();
    public CleanupConfig Cleanup { get; set; } = new();

    public static AppConfig Load(string? configPath = null)
    {
        var candidates = new List<string>();
        if (!string.IsNullOrEmpty(configPath)) candidates.Add(configPath);
        candidates.Add(Path.Combine(AppContext.BaseDirectory, "config.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "config.json"));
        candidates.Add(Path.Combine(Directory.GetCurrentDirectory(), "win_script", "config.json"));
        candidates.Add(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config.json")));

        var resolvedPath = candidates.FirstOrDefault(File.Exists);
        if (resolvedPath != null)
        {
            try
            {
                var json = File.ReadAllText(resolvedPath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                return JsonSerializer.Deserialize<AppConfig>(json, options) ?? new AppConfig();
            }
            catch
            {
                // Fallback to default config on parse error
            }
        }
        return new AppConfig();
    }

    public void Save(string? configPath = null)
    {
        var path = configPath ?? Path.Combine(AppContext.BaseDirectory, "config.json");
        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };
        var json = JsonSerializer.Serialize(this, options);
        File.WriteAllText(path, json);
    }
}

public class GitConfig
{
    public bool Enabled { get; set; } = true;
    public List<string> CustomSearchPaths { get; set; } = new();
    public bool ScanAllDrives { get; set; } = true;
    public bool FetchOnly { get; set; } = false;
    public List<string> IgnoredDirectories { get; set; } = new()
    {
        "node_modules", "bin", "obj", ".vs", "AppData", "vendor", "packages", "dist", "build", "target"
    };
    public int MaxSearchDepth { get; set; } = 5;
}

public class WingetConfig
{
    public bool Enabled { get; set; } = true;
    public bool IncludeUnknown { get; set; } = true;
    public bool AcceptAgreements { get; set; } = true;
}

public class DotnetConfig
{
    public bool Enabled { get; set; } = true;
    public bool UpdateGlobalTools { get; set; } = true;
    public bool UpdateWorkloads { get; set; } = true;
}

public class PythonConfig
{
    public bool Enabled { get; set; } = true;
    public bool UpdatePipPackages { get; set; } = true;
    public bool UpdatePipx { get; set; } = true;
    public bool UpdateUv { get; set; } = true;
}

public class NodeConfig
{
    public bool Enabled { get; set; } = true;
    public bool UpdateNpm { get; set; } = true;
    public bool UpdateYarn { get; set; } = true;
    public bool UpdatePnpm { get; set; } = true;
    public bool UpdateBun { get; set; } = true;
    public bool UpdateDeno { get; set; } = true;
}

public class CleanupConfig
{
    public bool Enabled { get; set; } = true;
    public bool ClearUserTemp { get; set; } = true;
    public bool ClearSystemTemp { get; set; } = true;
    public bool ClearDeliveryOptimization { get; set; } = true;
    public bool EmptyRecycleBin { get; set; } = false;
}
