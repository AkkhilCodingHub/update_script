using WinUpdate.Configuration;

namespace WinUpdate.Engine;

public interface IUpdateStep
{
    string Id { get; }
    string Name { get; }
    string Description { get; }
    StepCategory Category { get; }
    int Order { get; }

    /// <summary>
    /// Checks whether this step is available on the current machine (e.g. command installed, service running).
    /// </summary>
    Task<bool> IsAvailableAsync(AppConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the update step.
    /// </summary>
    Task<StepResult> ExecuteAsync(
        AppConfig config, 
        Action<string> logCallback, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the plan or what would be updated in dry-run mode.
    /// </summary>
    Task<string> GetDryRunPlanAsync(
        AppConfig config, 
        Action<string> logCallback, 
        CancellationToken cancellationToken = default);
}
