using System.Diagnostics;
using WinUpdate.Configuration;
using WinUpdate.Steps.Containers;
using WinUpdate.Steps.DevRuntimes;
using WinUpdate.Steps.Maintenance;
using WinUpdate.Steps.PackageManagers;
using WinUpdate.Steps.OS;

namespace WinUpdate.Engine;

public class UpdateOrchestrator
{
    private readonly List<IUpdateStep> _registeredSteps = new();
    private readonly AppConfig _config;

    public event Action<IUpdateStep>? StepStarting;
    public event Action<IUpdateStep, string>? StepLog;
    public event Action<IUpdateStep, StepResult>? StepCompleted;
    public event Action<List<StepResult>>? OrchestrationCompleted;

    public UpdateOrchestrator(AppConfig config)
    {
        _config = config;
        RegisterDefaultSteps();
    }

    private void RegisterDefaultSteps()
    {
        // System
        _registeredSteps.Add(new WindowsUpdateStep());
        _registeredSteps.Add(new WindowsDefenderStep());
        _registeredSteps.Add(new WslUpdateStep());

        // Package Managers
        _registeredSteps.Add(new WingetStep());
        _registeredSteps.Add(new ChocolateyStep());
        _registeredSteps.Add(new ScoopStep());
        _registeredSteps.Add(new VcpkgStep());

        // Dev Runtimes
        _registeredSteps.Add(new MiseStep());
        _registeredSteps.Add(new GitRepositoriesStep());
        _registeredSteps.Add(new DotnetToolsStep());
        _registeredSteps.Add(new PythonPipStep());
        _registeredSteps.Add(new NodeJsStep());
        _registeredSteps.Add(new RustStep());
        _registeredSteps.Add(new GoStep());
        _registeredSteps.Add(new PowerShellModulesStep());

        // Containers
        _registeredSteps.Add(new DockerStep());
        _registeredSteps.Add(new PodmanStep());

        // Maintenance
        _registeredSteps.Add(new SystemCleanupStep());
    }

    public IReadOnlyList<IUpdateStep> GetAllSteps() => _registeredSteps.OrderBy(s => s.Order).ToList();

    public async Task<List<IUpdateStep>> GetExecutableStepsAsync(CancellationToken cancellationToken = default)
    {
        var executable = new List<IUpdateStep>();

        foreach (var step in _registeredSteps.OrderBy(s => s.Order))
        {
            // Category filter
            if (_config.OnlyCategories.Count > 0 &&
                !_config.OnlyCategories.Any(c => string.Equals(c, step.Category.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            // Step inclusion / exclusion filter
            if (_config.OnlySteps.Count > 0 &&
                !_config.OnlySteps.Any(s => string.Equals(s, step.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(s, step.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (_config.SkipSteps.Count > 0 &&
                _config.SkipSteps.Any(s => string.Equals(s, step.Id, StringComparison.OrdinalIgnoreCase) || string.Equals(s, step.Name, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            try
            {
                if (await step.IsAvailableAsync(_config, cancellationToken))
                {
                    executable.Add(step);
                }
            }
            catch
            {
                // Step availability check threw, skip
            }
        }

        return executable;
    }

    public async Task<List<StepResult>> RunAsync(CancellationToken cancellationToken = default)
    {
        var steps = await GetExecutableStepsAsync(cancellationToken);
        var results = new List<StepResult>();

        foreach (var step in steps)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                results.Add(StepResult.Skipped(step.Id, step.Name, step.Category, "Cancelled by user"));
                continue;
            }

            StepStarting?.Invoke(step);

            var sw = Stopwatch.StartNew();
            var startTime = DateTime.Now;
            var logs = new List<string>();

            StepResult result;
            try
            {
                if (_config.DryRun)
                {
                    var plan = await step.GetDryRunPlanAsync(_config, line =>
                    {
                        logs.Add(line);
                        StepLog?.Invoke(step, line);
                    }, cancellationToken);

                    sw.Stop();
                    result = StepResult.Success(step.Id, step.Name, step.Category, "[Dry-Run] Plan generated successfully.");
                    result.OutputLogs.Add(plan);
                }
                else
                {
                    result = await step.ExecuteAsync(_config, line =>
                    {
                        logs.Add(line);
                        StepLog?.Invoke(step, line);
                    }, cancellationToken);
                    sw.Stop();
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                result = StepResult.Failed(step.Id, step.Name, step.Category, $"Execution error: {ex.Message}");
                logs.Add($"Exception: {ex}");
            }

            result.StartTime = startTime;
            result.EndTime = DateTime.Now;
            result.Duration = sw.Elapsed;
            foreach (var log in logs)
            {
                if (!result.OutputLogs.Contains(log))
                {
                    result.OutputLogs.Add(log);
                }
            }

            results.Add(result);
            StepCompleted?.Invoke(step, result);
        }

        OrchestrationCompleted?.Invoke(results);
        return results;
    }
}
