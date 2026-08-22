namespace WinUpdate.Engine;

public enum StepCategory
{
    System,
    PackageManagers,
    DevRuntimes,
    Containers,
    Maintenance
}

public enum StepStatus
{
    Pending,
    Running,
    Success,
    Warning,
    Failed,
    Skipped
}
