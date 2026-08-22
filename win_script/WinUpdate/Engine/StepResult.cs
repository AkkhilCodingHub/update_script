namespace WinUpdate.Engine;

public class StepResult
{
    public required string StepId { get; init; }
    public required string StepName { get; init; }
    public StepCategory Category { get; init; }
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public string Message { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; } = TimeSpan.Zero;
    public List<string> OutputLogs { get; } = new();
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public static StepResult Success(string stepId, string stepName, StepCategory category, string message = "Completed successfully")
    {
        return new StepResult
        {
            StepId = stepId,
            StepName = stepName,
            Category = category,
            Status = StepStatus.Success,
            Message = message
        };
    }

    public static StepResult Warning(string stepId, string stepName, StepCategory category, string message)
    {
        return new StepResult
        {
            StepId = stepId,
            StepName = stepName,
            Category = category,
            Status = StepStatus.Warning,
            Message = message
        };
    }

    public static StepResult Failed(string stepId, string stepName, StepCategory category, string message)
    {
        return new StepResult
        {
            StepId = stepId,
            StepName = stepName,
            Category = category,
            Status = StepStatus.Failed,
            Message = message
        };
    }

    public static StepResult Skipped(string stepId, string stepName, StepCategory category, string reason)
    {
        return new StepResult
        {
            StepId = stepId,
            StepName = stepName,
            Category = category,
            Status = StepStatus.Skipped,
            Message = reason
        };
    }
}
