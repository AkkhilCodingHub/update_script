using WinUpdate.Configuration;
using WinUpdate.Engine;
using WinUpdate.UI;

namespace WinUpdate;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var config = AppConfig.Load();
        var listSteps = false;
        var customConfigPath = (string?)null;

        // Parse CLI arguments
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            switch (arg)
            {
                case "--dry-run":
                case "-d":
                    config.DryRun = true;
                    break;
                case "--verbose":
                case "-v":
                    config.Verbose = true;
                    break;
                case "--list":
                case "-l":
                    listSteps = true;
                    break;
                case "--only":
                    if (i + 1 < args.Length)
                    {
                        var steps = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        config.OnlySteps.AddRange(steps);
                    }
                    break;
                case "--skip":
                    if (i + 1 < args.Length)
                    {
                        var steps = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        config.SkipSteps.AddRange(steps);
                    }
                    break;
                case "--category":
                case "-c":
                    if (i + 1 < args.Length)
                    {
                        var cats = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        config.OnlyCategories.AddRange(cats);
                    }
                    break;
                case "--cleanup-only":
                    config.OnlySteps.Clear();
                    config.OnlySteps.Add("cleanup");
                    break;
                case "--config":
                    if (i + 1 < args.Length)
                    {
                        customConfigPath = args[++i];
                        config = AppConfig.Load(customConfigPath);
                    }
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
            }
        }

        // CLI Listing Mode
        if (listSteps)
        {
            ConsoleRenderer.RenderBanner();
            await PrintAvailableStepsAsync(config);
            return 0;
        }

        // CLI Execution Mode
        ConsoleRenderer.RenderBanner();
        if (config.DryRun)
        {
            Console.WriteLine("\u001b[33m\u001b[1m[DRY-RUN MODE ENABLED] - No changes will be made.\u001b[0m\n");
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n\u001b[31m[!] Cancellation signal received. Stopping gracefully...\u001b[0m");
            cts.Cancel();
        };

        var orchestrator = new UpdateOrchestrator(config);

        orchestrator.StepStarting += step =>
        {
            ConsoleRenderer.RenderStepStart(step);
        };

        orchestrator.StepLog += (step, line) =>
        {
            ConsoleRenderer.RenderStepLog(step, line);
        };

        orchestrator.StepCompleted += (step, result) =>
        {
            ConsoleRenderer.RenderStepResult(step, result);
        };

        var results = await orchestrator.RunAsync(cts.Token);

        ConsoleRenderer.RenderSummaryTable(results);

        var hasFailures = results.Any(r => r.Status == StepStatus.Failed);
        return hasFailures ? 1 : 0;
    }

    private static async Task PrintAvailableStepsAsync(AppConfig config)
    {
        var orchestrator = new UpdateOrchestrator(config);
        var steps = orchestrator.GetAllSteps();

        var installedSteps = new List<IUpdateStep>();
        var notInstalledSteps = new List<IUpdateStep>();

        foreach (var step in steps)
        {
            if (await step.IsAvailableAsync(config))
                installedSteps.Add(step);
            else
                notInstalledSteps.Add(step);
        }

        Console.WriteLine("\u001b[1m\u001b[36mINSTALLED SOFTWARE DETECTED ON SYSTEM:\u001b[0m");
        Console.WriteLine($"{"ID",-22} {"Name",-34} {"Category",-16} {"Status"}");
        Console.WriteLine(new string('─', 82));

        foreach (var step in installedSteps)
        {
            Console.WriteLine($"{step.Id,-22} {step.Name,-34} {step.Category,-16} \u001b[32m\u001b[1m✔ Installed & Active\u001b[0m");
        }

        Console.WriteLine(new string('─', 82));
        Console.WriteLine($"\u001b[90m{installedSteps.Count} installed modules ready to update (out of {steps.Count} supported across Windows ecosystem).\u001b[0m\n");
    }

    private static void PrintHelp()
    {
        ConsoleRenderer.RenderBanner();
        Console.WriteLine("Usage: WinUpdate [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --dry-run, -d              Show what would be updated without applying changes");
        Console.WriteLine("  --list, -l                 List all installed modules detected on current machine");
        Console.WriteLine("  --only <step1,step2>       Run only specific steps (e.g. --only winget,git,dotnet)");
        Console.WriteLine("  --skip <step1,step2>       Skip specific steps (e.g. --skip windows-update)");
        Console.WriteLine("  --category, -c <name>      Run only specific category (System, PackageManagers, DevRuntimes, Containers, Maintenance)");
        Console.WriteLine("  --cleanup-only             Quickly run system temp and cache cleanup");
        Console.WriteLine("  --config <file>            Use custom configuration JSON file");
        Console.WriteLine("  --verbose, -v              Enable verbose logging");
        Console.WriteLine("  --help, -h                 Show this help message");
        Console.WriteLine();
    }
}
